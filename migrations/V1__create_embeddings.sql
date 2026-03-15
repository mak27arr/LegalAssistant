-- Migration: create embeddings table with pgvector column
-- Note: adjust vector dimension to match chosen embedding model (e.g., 384 for all-MiniLM-L6-v2)

CREATE TABLE IF NOT EXISTS embeddings (
  id uuid PRIMARY KEY,
  chunk_id uuid REFERENCES document_chunks(id) ON DELETE CASCADE,
  vector vector(384),
  model varchar,
  created_at timestamptz DEFAULT now()
);

-- Index for ANN search using ivfflat/hnsw can be created after populating the table
-- Example (pgvector HNSW):
-- SELECT ivfflat_create_index('embeddings', 'vector');
