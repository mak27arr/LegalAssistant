import type { FormEvent } from 'react';
import { useEffect, useRef, useState } from 'react';
import { createAskEventStream, getAskJob, submitAskJob } from '../shared/api/client';
import { generateClientId } from '../shared/lib/ids';
import { ensureStorageKey, readStorage, writeStorage } from '../shared/lib/storage';
import type { AskJobResponse } from '../shared/types/api';
import { StatusPill } from '../shared/ui/StatusPill';

const trackedAskJobsKey = 'legal-assistant.ask.jobs';
const conversationIdKey = 'legal-assistant.ask.conversation-id';
const actorKeyStorageKey = 'legal-assistant.ask.actor-key';
const maxRecentAskJobs = 5;

interface AskDraft {
  question: string;
  topK: number;
}

const initialDraft: AskDraft = {
  question: '',
  topK: 5
};

function isValidAskJob(value: unknown): value is AskJobResponse {
  if (!value || typeof value !== 'object') {
    return false;
  }

  const candidate = value as Partial<AskJobResponse>;
  const updatedAt =
    typeof candidate.updatedAt === 'string' && !Number.isNaN(Date.parse(candidate.updatedAt));

  return typeof candidate.jobId === 'string'
    && candidate.jobId.trim().length > 0
    && typeof candidate.status === 'string'
    && typeof candidate.question === 'string'
    && candidate.question.trim().length > 0
    && updatedAt;
}

function readStoredJobs(): AskJobResponse[] {
  return readStorage<unknown[]>(trackedAskJobsKey, []).filter(isValidAskJob);
}

export function AskPage() {
  const [draft, setDraft] = useState(initialDraft);
  const [jobs, setJobs] = useState<AskJobResponse[]>(readStoredJobs);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const eventSourcesRef = useRef<Map<string, EventSource>>(new Map());

  useEffect(() => {
    const sanitized = readStoredJobs();
    setJobs((current) => {
      const currentIds = current.map((job) => job.jobId).join('|');
      const sanitizedIds = sanitized.map((job) => job.jobId).join('|');

      if (currentIds === sanitizedIds) {
        return current;
      }

      return sanitized;
    });
  }, []);

  useEffect(() => {
    writeStorage(trackedAskJobsKey, jobs);
  }, [jobs]);

  useEffect(() => {
    jobs.forEach((job) => {
      if (!job.jobId) {
        return;
      }

      if (job.status !== 'Completed' && job.status !== 'Failed' && !eventSourcesRef.current.has(job.jobId)) {
        attachStream(job.jobId);
      }
    });

    return () => {
      eventSourcesRef.current.forEach((source) => source.close());
      eventSourcesRef.current.clear();
    };
  }, [jobs]);

  function updateJob(job: AskJobResponse) {
    if (!isValidAskJob(job)) {
      return;
    }

    setJobs((current) => {
      const next = [job, ...current.filter((item) => item.jobId !== job.jobId)];
      next.sort((left, right) => Date.parse(right.updatedAt) - Date.parse(left.updatedAt));
      return next.slice(0, maxRecentAskJobs);
    });
  }

  function attachStream(jobId: string) {
    if (!jobId.trim()) {
      return;
    }

    const source = createAskEventStream(jobId);
    eventSourcesRef.current.set(jobId, source);

    const upsertFromEvent = (event: MessageEvent<string>) => {
      const payload = JSON.parse(event.data) as AskJobResponse;
      updateJob(payload);

      if (payload.status === 'Completed' || payload.status === 'Failed') {
        source.close();
        eventSourcesRef.current.delete(jobId);
      }
    };

    source.addEventListener('queued', upsertFromEvent);
    source.addEventListener('inprogress', upsertFromEvent);
    source.addEventListener('completed', upsertFromEvent);
    source.addEventListener('failed', upsertFromEvent);
    source.onerror = async () => {
      try {
        const job = await getAskJob(jobId);
        updateJob(job);

        if (job.status === 'Completed' || job.status === 'Failed') {
          source.close();
          eventSourcesRef.current.delete(jobId);
        }
      } catch {
        source.close();
        eventSourcesRef.current.delete(jobId);
      }
    };
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    const actorKey = ensureStorageKey(actorKeyStorageKey, generateClientId);
    const conversationId = ensureStorageKey(conversationIdKey, generateClientId);
    const idempotencyKey = generateClientId();

    try {
      const submission = await submitAskJob(
        {
          question: draft.question,
          topK: draft.topK,
          conversationId
        },
        actorKey,
        idempotencyKey,
      );

      const createdJob = await getAskJob(submission.jobId);
      updateJob(createdJob);
      attachStream(submission.jobId);
      setDraft(initialDraft);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to submit the question.');
    } finally {
      setIsSubmitting(false);
    }
  }

  function clearJobs() {
    eventSourcesRef.current.forEach((source) => source.close());
    eventSourcesRef.current.clear();
    setJobs([]);
  }

  const visibleJobs = jobs.filter(isValidAskJob);

  return (
    <div className="page-grid">
      <section className="panel">
        <div className="panel-header">
          <div>
            <h2>Ask the assistant</h2>
            <p>Questions go through the async job pipeline and stream progress back over server-sent events.</p>
          </div>
          <span className="code-chip">POST /api/ask/async</span>
        </div>

        <form className="form-grid" onSubmit={handleSubmit}>
          <div className="field-group">
            <label htmlFor="ask-question">Question</label>
            <textarea
              id="ask-question"
              value={draft.question}
              onChange={(event) => setDraft((current) => ({ ...current, question: event.target.value }))}
              placeholder="What legal norm regulates termination of a lease agreement?"
              required
            />
          </div>

          <div className="field-group">
            <label htmlFor="ask-topk">Top K</label>
            <input
              id="ask-topk"
              min={1}
              max={20}
              step={1}
              type="number"
              value={draft.topK}
              onChange={(event) =>
                setDraft((current) => ({
                  ...current,
                  topK: Number.isNaN(Number(event.target.value)) ? current.topK : Number(event.target.value)
                }))
              }
            />
          </div>

          {error ? <div className="inline-error">{error}</div> : null}

          <div className="button-row">
            <button className="button-primary" disabled={isSubmitting} type="submit">
              {isSubmitting ? 'Submitting...' : 'Ask question'}
            </button>
            {jobs.length > 0 ? (
              <button className="button-secondary" type="button" onClick={clearJobs}>
                Clear local history
              </button>
            ) : null}
          </div>
        </form>
      </section>

      <section className="answer-card">
        <div className="stack-header">
          <div>
            <h2>Recent ask jobs</h2>
            <p>Realtime status comes from SSE, while completed jobs keep their final answer in local history.</p>
          </div>
          <span className="code-chip">{'GET /api/ask/jobs/{jobId}/events'}</span>
        </div>

        {visibleJobs.length === 0 ? (
          <div className="inline-info">No ask jobs yet. Submit a question to open the first realtime stream.</div>
        ) : (
          <div className="event-list">
            {visibleJobs.map((job) => (
              <article className="event-item" key={job.jobId}>
                <div className="event-item-header">
                  <div>
                    <h3>{job.question}</h3>
                    <p className="event-meta">Job ID: {job.jobId}</p>
                  </div>
                  <StatusPill status={job.status} />
                </div>

                <p className="event-meta">Updated: {new Date(job.updatedAt).toLocaleString()}</p>
                <p className="event-meta">Conversation: {job.conversationId ?? 'none'}</p>

                {job.error ? <div className="inline-error">{job.error}</div> : null}

                {job.result ? (
                  <div className="event-answer">
                    <strong>Answer</strong>
                    <p>{job.result.answer}</p>
                    <p className="event-meta">Grounded: {job.result.isGrounded ? 'yes' : 'no'}</p>
                  </div>
                ) : null}
              </article>
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
