import { useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { getDocument } from '../shared/api/client';
import type { DocumentDetailsResponse } from '../shared/types/api';
import { StatusPill } from '../shared/ui/StatusPill';

export function DocumentDetailsPage() {
  const navigate = useNavigate();
  const { documentId } = useParams<{ documentId: string }>();
  const [document, setDocument] = useState<DocumentDetailsResponse | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    if (!documentId) {
      setError('Document id is missing.');
      setIsLoading(false);
      return;
    }

    void loadDocument(documentId);
  }, [documentId]);

  async function loadDocument(id: string) {
    setIsLoading(true);
    setError(null);

    try {
      setDocument(await getDocument(id));
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to load document details.');
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h2>Document details</h2>
          <p>Detail view backed by the new document details endpoint.</p>
        </div>
      </div>

      <div className="button-row">
        <Link className="button-secondary" to="/documents">
          Back to documents database
        </Link>
      </div>

      {error ? <div className="inline-error">{error}</div> : null}
      {isLoading ? <div className="inline-info">Loading document details...</div> : null}

      {!isLoading && !error && document ? (
        <div className="details-grid">
          <div className="metric-card">
            <strong>{document.title}</strong>
            <span>Title</span>
          </div>
          <div className="metric-card">
            <strong>{document.version}</strong>
            <span>Version</span>
          </div>
          <div className="metric-card">
            {document.chunkCount > 0 ? (
              <button className="metric-link" type="button" onClick={() => navigate(`/documents/${document.id}/chunks`)}>
                {document.chunkCount}
              </button>
            ) : (
              <strong>{document.chunkCount}</strong>
            )}
            <span>Chunks</span>
          </div>
          <div className="metric-card">
            {document.processingStatus ? <StatusPill status={document.processingStatus} /> : <strong>Unknown</strong>}
            <span>Processing status</span>
          </div>
          <div className="metric-card">
            <strong>{document.completedEmbeddingCount} / {document.embeddingCount}</strong>
            <span>Embeddings ready</span>
          </div>
          {document.failedEmbeddingCount > 0 ? (
            <div className="metric-card">
              <strong>{document.failedEmbeddingCount}</strong>
              <span>Embeddings failed</span>
            </div>
          ) : null}
          <div className="metric-card">
            <strong>{new Date(document.createdAt).toLocaleDateString()}</strong>
            <span>Created</span>
          </div>

          <div className="detail-card">
            <h3>Identifiers</h3>
            <p className="tracked-meta">Document ID: {document.id}</p>
            <p className="tracked-meta">Updated: {new Date(document.updatedAt).toLocaleString()}</p>
          </div>

          <div className="detail-card">
            <h3>Source URL</h3>
            <p className="tracked-meta source-break">{document.url}</p>
          </div>
        </div>
      ) : null}
    </section>
  );
}
