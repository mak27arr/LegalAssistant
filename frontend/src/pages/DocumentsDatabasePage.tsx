import { useEffect, useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { getDocuments } from '../shared/api/client';
import type { DocumentListItemResponse } from '../shared/types/api';

export function DocumentsDatabasePage() {
  const navigate = useNavigate();
  const [documents, setDocuments] = useState<DocumentListItemResponse[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    void loadDocuments();
  }, []);

  async function loadDocuments() {
    setIsLoading(true);
    setError(null);

    try {
      setDocuments(await getDocuments());
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
        <span className="code-chip">GET /api/documents</span>
      </div>

      <div className="button-row">
        <Link className="button-secondary" to="/">
          Back to intake
        </Link>
        <button className="button-secondary" type="button" onClick={() => void loadDocuments()}>
          {isLoading ? 'Refreshing...' : 'Refresh list'}
        </button>
      </div>

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
  );
}
