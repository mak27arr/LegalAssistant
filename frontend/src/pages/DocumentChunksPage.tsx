import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { getChunk, getDocument, getDocumentChunks } from '../shared/api/client';
import type { ChunkDetailsResponse, ChunkListItemResponse, DocumentDetailsResponse } from '../shared/types/api';

interface ChunkModalState {
  isOpen: boolean;
  isLoading: boolean;
  error: string | null;
  chunk: ChunkDetailsResponse | null;
}

const initialModalState: ChunkModalState = {
  isOpen: false,
  isLoading: false,
  error: null,
  chunk: null
};

export function DocumentChunksPage() {
  const { documentId } = useParams<{ documentId: string }>();
  const [document, setDocument] = useState<DocumentDetailsResponse | null>(null);
  const [chunks, setChunks] = useState<ChunkListItemResponse[]>([]);
  const [page, setPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const [error, setError] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [modal, setModal] = useState<ChunkModalState>(initialModalState);

  useEffect(() => {
    if (!documentId) {
      setError('Document id is missing.');
      setIsLoading(false);
      return;
    }

    void loadPage(documentId, 1);
  }, [documentId]);

  async function loadPage(id: string, nextPage: number) {
    setIsLoading(true);
    setError(null);

    try {
      const [documentDetails, chunkPage] = await Promise.all([
        getDocument(id),
        getDocumentChunks(id, nextPage, 20)
      ]);

      setDocument(documentDetails);
      setChunks(chunkPage.items);
      setPage(chunkPage.page);
      setTotalPages(chunkPage.totalPages);
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Unable to load document chunks.');
    } finally {
      setIsLoading(false);
    }
  }

  async function openChunk(chunkId: string) {
    setModal({
      isOpen: true,
      isLoading: true,
      error: null,
      chunk: null
    });

    try {
      const chunk = await getChunk(chunkId);
      setModal({
        isOpen: true,
        isLoading: false,
        error: null,
        chunk
      });
    } catch (requestError) {
      setModal({
        isOpen: true,
        isLoading: false,
        error: requestError instanceof Error ? requestError.message : 'Unable to load chunk details.',
        chunk: null
      });
    }
  }

  function closeModal() {
    setModal(initialModalState);
  }

  return (
    <section className="panel">
      <div className="panel-header">
        <div>
          <h2>Document chunks</h2>
          <p>Chunk list for the selected document. Click a chunk row to open its content.</p>
        </div>
      </div>

      <div className="button-row">
        <Link className="button-secondary" to={documentId ? `/documents/${documentId}` : '/documents'}>
          Back to document details
        </Link>
      </div>

      {error ? <div className="inline-error">{error}</div> : null}

      {document ? (
        <div className="detail-card compact-card">
          <h3>{document.title}</h3>
          <p className="tracked-meta">Document ID: {document.id}</p>
          <p className="tracked-meta">Total chunks: {document.chunkCount}</p>
        </div>
      ) : null}

      {chunks.length === 0 ? (
        <div className="inline-info">
          {isLoading ? 'Loading chunks...' : 'This document has no chunks to display.'}
        </div>
      ) : (
        <>
          <div className="table-shell">
            <table className="data-table">
              <thead>
                <tr>
                  <th>Chunk</th>
                  <th>Range</th>
                  <th>Embedding</th>
                  <th>Attempts</th>
                  <th>Preview</th>
                </tr>
              </thead>
              <tbody>
                {chunks.map((chunk) => (
                  <tr
                    className="table-row-button"
                    key={chunk.chunkId}
                    onClick={() => void openChunk(chunk.chunkId)}
                    onKeyDown={(event) => {
                      if (event.key === 'Enter' || event.key === ' ') {
                        event.preventDefault();
                        void openChunk(chunk.chunkId);
                      }
                    }}
                    role="button"
                    tabIndex={0}
                  >
                    <td>#{chunk.chunkIndex}</td>
                    <td>{chunk.charRange}</td>
                    <td>
                      <div>{chunk.embeddingStatus}</div>
                      {chunk.embeddingLastError ? <div className="table-subtitle">{chunk.embeddingLastError}</div> : null}
                    </td>
                    <td>{chunk.embeddingAttemptCount}</td>
                    <td>
                      <div className="table-subtitle">{chunk.preview}</div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className="button-row">
            <button
              className="button-secondary"
              disabled={!documentId || page <= 1 || isLoading}
              type="button"
              onClick={() => documentId && void loadPage(documentId, page - 1)}
            >
              Previous
            </button>
            <span className="page-indicator">
              Page {page} of {totalPages}
            </span>
            <button
              className="button-secondary"
              disabled={!documentId || page >= totalPages || isLoading}
              type="button"
              onClick={() => documentId && void loadPage(documentId, page + 1)}
            >
              Next
            </button>
          </div>
        </>
      )}

      {modal.isOpen ? (
        <div className="modal-backdrop" onClick={closeModal} role="presentation">
          <div
            className="modal-card"
            onClick={(event) => event.stopPropagation()}
            onKeyDown={(event) => {
              if (event.key === 'Escape') {
                closeModal();
              }
            }}
            role="dialog"
            aria-modal="true"
            tabIndex={-1}
          >
            <div className="panel-header">
              <div>
                <h3>Chunk content</h3>
                <p>Loaded from the dedicated chunk details endpoint.</p>
              </div>
              <button className="button-secondary" type="button" onClick={closeModal}>
                Close
              </button>
            </div>

            {modal.error ? <div className="inline-error">{modal.error}</div> : null}
            {modal.isLoading ? <div className="inline-info">Loading chunk content...</div> : null}

            {modal.chunk ? (
              <div className="modal-content">
                <p className="tracked-meta">Chunk ID: {modal.chunk.chunkId}</p>
                <p className="tracked-meta">Range: {modal.chunk.charRange}</p>
                <p className="tracked-meta">Source URL: {modal.chunk.sourceUrl}</p>
                <p className="tracked-meta">Embedding: {modal.chunk.embeddingStatus} (attempts: {modal.chunk.embeddingAttemptCount})</p>
                {modal.chunk.embeddingLastError ? <div className="inline-error">{modal.chunk.embeddingLastError}</div> : null}
                <pre className="chunk-text">{modal.chunk.text}</pre>
              </div>
            ) : null}
          </div>
        </div>
      ) : null}
    </section>
  );
}
