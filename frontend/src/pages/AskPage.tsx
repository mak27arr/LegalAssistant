import type { FormEvent } from 'react';
import { useEffect, useRef, useState } from 'react';
import { useAuth } from '../features/auth/AuthContext';
import { createAskEventStream, getAskJob, getCurrentUser, submitAskJob } from '../shared/api/client';
import { generateClientId } from '../shared/lib/ids';
import { ensureStorageKey, readStorage, writeStorage } from '../shared/lib/storage';
import type { AskJobResponse } from '../shared/types/api';
import { StatusPill } from '../shared/ui/StatusPill';

const trackedAskJobsKey = 'legal-assistant.ask.jobs';
const conversationIdKey = 'legal-assistant.ask.conversation-id';
const maxRecentAskJobs = 5;

interface AskDraft {
  question: string;
  topK: number;
}

const initialDraft: AskDraft = {
  question: '',
  topK: 5
};

function normalizeAskJobResponse(value: unknown): AskJobResponse | null {
  if (!value || typeof value !== 'object') {
    return null;
  }

  const candidate = value as Record<string, unknown>;
  const result =
    candidate.result && typeof candidate.result === 'object'
      ? candidate.result
      : candidate.Result && typeof candidate.Result === 'object'
        ? candidate.Result
        : null;
  const resultCandidate = result as Record<string, unknown> | null;
  const normalizedResult =
    resultCandidate
      ? {
          question:
            typeof resultCandidate.Question === 'string'
              ? resultCandidate.Question
              : typeof resultCandidate.question === 'string'
                ? resultCandidate.question
                : '',
          answer:
            typeof resultCandidate.Answer === 'string'
              ? resultCandidate.Answer
              : typeof resultCandidate.answer === 'string'
                ? resultCandidate.answer
                : '',
          isGrounded:
            typeof resultCandidate.IsGrounded === 'boolean'
              ? resultCandidate.IsGrounded
              : typeof resultCandidate.isGrounded === 'boolean'
                ? resultCandidate.isGrounded
                : false
        }
      : null;

  return {
    jobId: typeof candidate.jobId === 'string' ? candidate.jobId : typeof candidate.JobId === 'string' ? candidate.JobId : '',
    status: typeof candidate.status === 'string' ? candidate.status : typeof candidate.Status === 'string' ? candidate.Status : '',
    question: typeof candidate.question === 'string' ? candidate.question : typeof candidate.Question === 'string' ? candidate.Question : '',
    topK: typeof candidate.topK === 'number' ? candidate.topK : typeof candidate.TopK === 'number' ? candidate.TopK : 0,
    conversationId:
      typeof candidate.conversationId === 'string'
        ? candidate.conversationId
        : typeof candidate.ConversationId === 'string'
          ? candidate.ConversationId
          : null,
    error: typeof candidate.error === 'string' ? candidate.error : typeof candidate.Error === 'string' ? candidate.Error : null,
    result: normalizedResult,
    createdAt:
      typeof candidate.createdAt === 'string'
        ? candidate.createdAt
        : typeof candidate.CreatedAt === 'string'
          ? candidate.CreatedAt
          : '',
    updatedAt:
      typeof candidate.updatedAt === 'string'
        ? candidate.updatedAt
        : typeof candidate.UpdatedAt === 'string'
          ? candidate.UpdatedAt
          : ''
  };
}

function getJobTimestamp(job: AskJobResponse) {
  const updatedAt = Date.parse(job.updatedAt);
  if (!Number.isNaN(updatedAt)) {
    return updatedAt;
  }

  const createdAt = Date.parse(job.createdAt);
  return Number.isNaN(createdAt) ? 0 : createdAt;
}

function getJobStatusRank(status: string) {
  switch (status) {
    case 'Queued':
      return 1;
    case 'InProgress':
      return 2;
    case 'Completed':
    case 'Failed':
      return 3;
    default:
      return 0;
  }
}

function shouldReplaceJob(current: AskJobResponse | undefined, incoming: AskJobResponse) {
  if (!current) {
    return true;
  }

  const currentTimestamp = getJobTimestamp(current);
  const incomingTimestamp = getJobTimestamp(incoming);
  if (incomingTimestamp !== currentTimestamp) {
    return incomingTimestamp > currentTimestamp;
  }

  const currentRank = getJobStatusRank(current.status);
  const incomingRank = getJobStatusRank(incoming.status);
  if (incomingRank !== currentRank) {
    return incomingRank > currentRank;
  }

  if (!current.result && incoming.result) {
    return true;
  }

  if (current.result && !incoming.result) {
    return false;
  }

  return true;
}

function mergeJobs(current: AskJobResponse | undefined, incoming: AskJobResponse): AskJobResponse {
  if (!current) {
    return incoming;
  }

  if (!shouldReplaceJob(current, incoming)) {
    return current;
  }

  return {
    ...current,
    ...incoming,
    error: incoming.error ?? current.error,
    result: incoming.result ?? current.result
  };
}

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
  const { status, refreshSession } = useAuth();
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
      if ((job.status === 'Completed' || job.status === 'Failed') && eventSourcesRef.current.has(job.jobId)) {
        eventSourcesRef.current.get(job.jobId)?.close();
        eventSourcesRef.current.delete(job.jobId);
      }
    });
  }, [jobs]);

  useEffect(() => {
    return () => {
      eventSourcesRef.current.forEach((source) => source.close());
      eventSourcesRef.current.clear();
    };
  }, []);

  function updateJob(job: AskJobResponse) {
    if (!isValidAskJob(job)) {
      return;
    }

    setJobs((current) => {
      const existing = current.find((item) => item.jobId === job.jobId);
      const nextJob = mergeJobs(existing, job);
      const next = [nextJob, ...current.filter((item) => item.jobId !== job.jobId)];
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
      let payload: AskJobResponse | null = null;

      try {
        payload = normalizeAskJobResponse(JSON.parse(event.data));
      } catch {
        return;
      }

      if (!payload) {
        return;
      }

      updateJob(payload);

      if (payload.status === 'Completed' || payload.status === 'Failed') {
        source.close();
        eventSourcesRef.current.delete(jobId);
      }
    };

    source.onmessage = upsertFromEvent;
    source.addEventListener('queued', upsertFromEvent);
    source.addEventListener('inprogress', upsertFromEvent);
    source.addEventListener('completed', upsertFromEvent);
    source.addEventListener('failed', upsertFromEvent);
    source.onerror = async () => {
      try {
        await getCurrentUser();
      } catch {
        await refreshSession();
        source.close();
        eventSourcesRef.current.delete(jobId);
      }
    };
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    const conversationId = ensureStorageKey(conversationIdKey, generateClientId);
    const idempotencyKey = generateClientId();

    try {
      const submission = await submitAskJob(
        {
          question: draft.question,
          topK: draft.topK,
          conversationId
        },
        idempotencyKey,
      );

      const createdJob = await getAskJob(submission.jobId);
      updateJob(createdJob);
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
  const isAuthenticated = status === 'authenticated';

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
            <button className="button-primary" disabled={isSubmitting || !isAuthenticated} type="submit">
              {isSubmitting ? 'Submitting...' : 'Ask question'}
            </button>
            {!isAuthenticated ? <span className="inline-info">Sign in is required for live ask operations.</span> : null}
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
