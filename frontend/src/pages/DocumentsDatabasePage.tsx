import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { getDocuments } from '../shared/api/client';
import type { DocumentListItemResponse } from '../shared/types/api';
import { StatusPill } from '../shared/ui/StatusPill';

export function DocumentsDatabasePage() {
  const navigate = useNavigate();
  const [documents, setDocuments] = useState<DocumentListItemResponse[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [totalItems, setTotalItems] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    void loadDocuments(page);
  }, [page]);

  async function loadDocuments(nextPage: number) {
    setIsLoading(true);
    setError(null);

    try {
      const result = await getDocuments(nextPage, 20);
      setDocuments(result.items);
      setPage(result.page);
      setTotalPages(result.totalPages);
      setTotalItems(result.totalItems);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to load documents.');
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h2>Documents in database</h2>
          <p>Browse all ingested documents from the dedicated list endpoint.</p>
        </div>
      </div>

      <div className="button-row">
        <Link className="button-secondary" to="/">
          Back to intake
        </Link>
        <button className="button-secondary" type="button" onClick={() => void loadDocuments(page)}>
          {isLoading ? 'Refreshing...' : 'Refresh list'}
        </button>
      </div>

      <p className="muted">Total documents: {totalItems}</p>

      {error ? <div className="inline-error">{error}</div> : null}

      {documents.length === 0 ? (
        <div className="inline-info">
          {isLoading ? 'Loading documents...' : 'No documents returned by the backend yet.'}
        </div>
      ) : (
        <div className="table-shell">
          <table className="data-table">
            <thead>
              <tr>
                <th>Title</th>
                <th>Status</th>
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
                    <td>
                      {document.processingStatus ? <StatusPill status={document.processingStatus} /> : <span className="muted">Unknown</span>}
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

      <div className="button-row">
        <button
          className="button-secondary"
          disabled={page <= 1 || isLoading}
          type="button"
          onClick={() => setPage((current) => Math.max(1, current - 1))}
        >
          Previous
        </button>
        <span className="page-indicator">
          Page {page} of {totalPages}
        </span>
        <button
          className="button-secondary"
          disabled={page >= totalPages || isLoading}
          type="button"
          onClick={() => setPage((current) => Math.min(totalPages, current + 1))}
        >
          Next
        </button>
      </div>
    </section>
  );
}
