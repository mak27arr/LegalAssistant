import type { FormEvent } from 'react';
import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { createDocument, getDocumentStats, getDocuments, getJob } from '../shared/api/client';
import { generateClientId } from '../shared/lib/ids';
import { readStorage, writeStorage } from '../shared/lib/storage';
import type { CreateDocumentRequest, DocumentListItemResponse, DocumentStatsResponse } from '../shared/types/api';
import { MetricCard } from '../shared/ui/MetricCard';
import { StatusPill } from '../shared/ui/StatusPill';

const trackedDocumentsKey = 'legal-assistant.documents.tracked';

interface TrackedDocument {
  documentId: string;
  jobId: string;
  title: string;
  url: string;
  submittedAt: string;
  status: string;
  jobType?: string;
}

const initialForm: CreateDocumentRequest = {
  title: '',
  url: '',
  content: '',
  metadata: {}
};

function buildTitleFromUrl(rawUrl: string): string {
  const fallback = 'Imported document';

  try {
    const parsed = new URL(rawUrl);
    const segments = parsed.pathname.split('/').filter(Boolean);
    const lastSegment = segments.length > 0 ? segments[segments.length - 1] : undefined;
    return lastSegment ? decodeURIComponent(lastSegment) : parsed.hostname;
  } catch {
    return rawUrl.trim() || fallback;
  }
}

export function DocumentsPage() {
  const navigate = useNavigate();
  const [form, setForm] = useState<CreateDocumentRequest>(initialForm);
  const [stats, setStats] = useState<DocumentStatsResponse | null>(null);
  const [documents, setDocuments] = useState<DocumentListItemResponse[]>([]);
  const [tracked, setTracked] = useState<TrackedDocument[]>(() => readStorage(trackedDocumentsKey, [] as TrackedDocument[]));
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isRefreshingStats, setIsRefreshingStats] = useState(false);
  const [isLoadingDocuments, setIsLoadingDocuments] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [statsError, setStatsError] = useState<string | null>(null);
  const [documentsError, setDocumentsError] = useState<string | null>(null);

  useEffect(() => {
    void refreshStats();
    void refreshDocuments();
  }, []);

  useEffect(() => {
    writeStorage(trackedDocumentsKey, tracked);
  }, [tracked]);

  useEffect(() => {
    if (tracked.length === 0) {
      return;
    }

    const poll = () => {
      const activeItems = tracked.filter((item) => item.status !== 'Completed' && item.status !== 'Failed');
      if (activeItems.length === 0) {
        return;
      }

      void Promise.all(
        activeItems.map(async (item) => {
          try {
            const job = await getJob(item.jobId);
            return { ...item, status: job.status, jobType: job.type };
          } catch {
            return item;
          }
        }),
      ).then((results) => {
        setTracked((current) =>
          current.map((item) => results.find((candidate) => candidate.jobId === item.jobId) ?? item),
        );
      });
    };

    void poll();
    const intervalId = window.setInterval(poll, 4000);

    return () => {
      window.clearInterval(intervalId);
    };
  }, [tracked]);

  async function refreshStats() {
    setIsRefreshingStats(true);
    setStatsError(null);

    try {
      setStats(await getDocumentStats());
    } catch (requestError) {
      setStatsError(requestError instanceof Error ? requestError.message : 'Unable to load document stats.');
    } finally {
      setIsRefreshingStats(false);
    }
  }

  async function refreshDocuments() {
    setIsLoadingDocuments(true);
    setDocumentsError(null);

    try {
      setDocuments(await getDocuments());
    } catch (requestError) {
      setDocumentsError(requestError instanceof Error ? requestError.message : 'Unable to load documents.');
    } finally {
      setIsLoadingDocuments(false);
    }
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);

    setIsSubmitting(true);

    try {
      const payload: CreateDocumentRequest = {
        title: buildTitleFromUrl(form.url),
        url: form.url.trim(),
        content: '',
        metadata: {}
      };

      const response = await createDocument(payload);
      const submittedItem: TrackedDocument = {
        documentId: response.documentId,
        jobId: response.jobId,
        title: payload.title || `Document ${generateClientId().slice(0, 8)}`,
        url: payload.url,
        submittedAt: new Date().toISOString(),
        status: 'Queued'
      };

      setTracked((current) => [submittedItem, ...current].slice(0, 12));
      setForm(initialForm);
      await refreshStats();
      await refreshDocuments();
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to submit document.');
    } finally {
      setIsSubmitting(false);
    }
  }

  function resetTracked() {
    setTracked([]);
  }

  return (
    <div className="page-grid">
      <section className="panel">
        <div className="panel-header">
          <div>
            <h2>Document intake</h2>
            <p>Submit a document by URL only. The frontend derives a title automatically and tracks the ingest job.</p>
          </div>
          <span className="code-chip">POST /api/documents</span>
        </div>

        <form className="form-grid" onSubmit={handleSubmit}>
          <div className="field-group">
            <label htmlFor="document-url">Source URL</label>
            <input
              id="document-url"
              value={form.url}
              onChange={(event) => setForm((current) => ({ ...current, url: event.target.value }))}
              placeholder="https://zakon.rada.gov.ua/..."
              required
            />
          </div>

          {error ? <div className="inline-error">{error}</div> : null}

          <div className="button-row">
            <button className="button-primary" disabled={isSubmitting} type="submit">
              {isSubmitting ? 'Submitting...' : 'Add document'}
            </button>
            <button className="button-secondary" disabled={isSubmitting} type="button" onClick={() => void refreshStats()}>
              {isRefreshingStats ? 'Refreshing...' : 'Refresh stats'}
            </button>
            <button className="button-secondary" disabled={isSubmitting} type="button" onClick={() => void refreshDocuments()}>
              {isLoadingDocuments ? 'Refreshing docs...' : 'Refresh documents'}
            </button>
          </div>
        </form>
      </section>

      <div className="stack">
        <section className="status-card">
          <div className="stack-header">
            <div>
              <h2>System snapshot</h2>
              <p>Aggregate counts from the backend. No list endpoint required.</p>
            </div>
            <span className="code-chip">GET /api/documents/stats</span>
          </div>

          {statsError ? <div className="inline-error">{statsError}</div> : null}

          <div className="stats-grid">
            <MetricCard value={stats?.totalDocuments ?? '—'} label="Documents" />
            <MetricCard value={stats?.queuedJobs ?? '—'} label="Queued" />
            <MetricCard value={stats?.inProgressJobs ?? '—'} label="In progress" />
            <MetricCard value={stats?.completedJobs ?? '—'} label="Completed" />
            <MetricCard value={stats?.failedJobs ?? '—'} label="Failed" />
          </div>
        </section>

        <section className="list-card">
          <div className="stack-header">
            <div>
              <h3>Documents in database</h3>
              <p>Loaded from the new list endpoint. Click a row to open document details.</p>
            </div>
            <span className="code-chip">GET /api/documents</span>
          </div>

          {documentsError ? <div className="inline-error">{documentsError}</div> : null}

          {documents.length === 0 ? (
            <div className="inline-info">
              {isLoadingDocuments ? 'Loading documents...' : 'No documents returned by the backend yet.'}
            </div>
          ) : (
            <div className="table-shell">
              <table className="data-table">
                <thead>
                  <tr>
                    <th>Title</th>
                    <th>Version</th>
                    <th>Chunks</th>
                    <th>Updated</th>
                  </tr>
                </thead>
                <tbody>
                  {documents.map((document) => (
                    <tr
                      className="table-row-button"
                      key={document.id}
                      onClick={() => navigate(`/documents/${document.id}`)}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter' || event.key === ' ') {
                          event.preventDefault();
                          navigate(`/documents/${document.id}`);
                        }
                      }}
                      role="button"
                      tabIndex={0}
                    >
                      <td>
                        <strong>{document.title}</strong>
                        <div className="table-subtitle">{document.url}</div>
                      </td>
                      <td>{document.version}</td>
                      <td>{document.chunkCount}</td>
                      <td>{new Date(document.updatedAt).toLocaleString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>

        <section className="list-card">
          <div className="stack-header">
            <div>
              <h3>Tracked submissions</h3>
              <p>The UI keeps the latest jobs it created, so we can stay within the current API contract.</p>
            </div>
            {tracked.length > 0 ? (
              <button className="button-secondary" type="button" onClick={resetTracked}>
                Clear local list
              </button>
            ) : null}
          </div>

          {tracked.length === 0 ? (
            <div className="inline-info">No frontend-tracked submissions yet. Add a document to start monitoring jobs.</div>
          ) : (
            <div className="tracked-list">
              {tracked.map((item) => (
                <article className="tracked-item" key={item.jobId}>
                  <div className="tracked-item-header">
                    <div>
                      <h3>{item.title}</h3>
                      <p className="tracked-meta">{item.url}</p>
                    </div>
                    <StatusPill status={item.status} />
                  </div>
                  <p className="tracked-meta">Document ID: {item.documentId}</p>
                  <p className="tracked-meta">Job ID: {item.jobId}</p>
                  <p className="tracked-meta">Submitted: {new Date(item.submittedAt).toLocaleString()}</p>
                  {item.jobType ? <p className="tracked-meta">Job type: {item.jobType}</p> : null}
                </article>
              ))}
            </div>
          )}
        </section>
      </div>
    </div>
  );
}
