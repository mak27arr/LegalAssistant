-- This file contains Postgres initialization SQL for pgvector and tables.
-- It is commented out to avoid build-time SQL parsing errors in the IDE.
-- To enable for local Postgres initialization, uncomment the SQL below.

-- CREATE EXTENSION IF NOT EXISTS vector;

-- CREATE TABLE IF NOT EXISTS documents (
--   id uuid PRIMARY KEY,
--   title text,
--   url text,
--   content text,
--   metadata jsonb,
--   version int DEFAULT 1,
--   is_deleted boolean DEFAULT false,
--   created_at timestamptz DEFAULT now(),
--   updated_at timestamptz DEFAULT now()
-- );

-- CREATE TABLE IF NOT EXISTS document_chunks (
--   id uuid PRIMARY KEY,
--   document_id uuid REFERENCES documents(id) ON DELETE CASCADE,
--   chunk_index int,
--   text text,
--   char_range text,
--   source_url text,
--   embedding vector(768),
--   created_at timestamptz DEFAULT now()
-- );

-- ANN index for semantic search on the real table used by the app.
-- CREATE INDEX IF NOT EXISTS ix_document_chunks_embedding_hnsw
--   ON document_chunks
--   USING hnsw (embedding vector_l2_ops)
--   WHERE embedding IS NOT NULL;
-- );
CREATE EXTENSION IF NOT EXISTS vector;
