-- Legacy compatibility migration.
-- The application stores vectors on document_chunks.embedding (vector(768)),
-- not in a separate embeddings table.

CREATE INDEX IF NOT EXISTS ix_document_chunks_embedding_hnsw
  ON document_chunks
  USING hnsw (embedding vector_l2_ops)
  WHERE embedding IS NOT NULL;
