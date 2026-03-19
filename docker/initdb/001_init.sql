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
--   created_at timestamptz DEFAULT now()
-- );

-- embeddings table uses vector type, developer must set dim appropriately
-- CREATE TABLE IF NOT EXISTS embeddings (
--   id uuid PRIMARY KEY,
--   chunk_id uuid REFERENCES document_chunks(id) ON DELETE CASCADE,
--   vector vector(384),
--   model varchar,
--   created_at timestamptz DEFAULT now()
-- );
CREATE EXTENSION IF NOT EXISTS vector;

-- embeddings table uses vector type, developer must set dim appropriately
-- CREATE TABLE IF NOT EXISTS embeddings (
--   id uuid PRIMARY KEY,
--   chunk_id uuid REFERENCES document_chunks(id) ON DELETE CASCADE,
--   vector vector(384),
--   model varchar,
--   created_at timestamptz DEFAULT now()
-- );
