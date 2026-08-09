# Detailed Implementation Plan

**Goal:** implement an ASP.NET Core RAG service using PostgreSQL + pgvector and a local or remote LLM for legal queries, progressing from MVP to a production-ready roadmap.

## 1. Component architecture

- Backend: ASP.NET Core Web API, with services registered through dependency injection.
  - `DocumentService` - CRUD, caching, and parsing.
  - `EmbeddingService` - embedding generation and batching.
  - `VectorDbLayer` - pgvector/SQL wrapper and approximate-nearest-neighbour search.
  - `RagPipelineService` - retrieve, augment, and build prompts.
  - `LlmService` - adapter for Ollama, GPT4All, and other providers.
  - `AdminService` - reindexing, metrics, and feedback.

## 2. API endpoints (OpenAPI/Swagger)

- `POST /ask` - body: `{ query, top_k?, filters? }`; response: `{ answer, sources[], score }`.
- `POST /documents` - adds a document from a URL or file.
- `GET/PUT/DELETE /documents/{id}` - reads, updates, or soft-deletes metadata and content.
- `POST /admin/reindex` - starts asynchronous reindexing.
- `GET /health` - checks the database, vector index, and LLM.
- `POST /feedback` - records human feedback.

## 3. Database schema (PostgreSQL + pgvector)

- `documents`:
  - `id UUID PK`, `title text`, `url text`, `content text`, `metadata jsonb`, `created_at`, `updated_at`, `version int`, `is_deleted bool`.
- `document_chunks`:
  - `id UUID PK`, `document_id FK`, `chunk_index int`, `text text`, `char_range int4range`, `source_url text`, `created_at`.
- `embeddings`:
  - `id UUID PK`, `chunk_id FK`, `vector vector(<dim>)`, `model varchar`, `created_at`.

Indexes: pgvector HNSW or IVF. Store the embedding dimension in the migration and define a reindexing plan when the embedding model changes.

## 4. Ingestion pipeline

- Support URL, PDF, HTML, and raw text.
- Steps: fetch -> extract (Tika, PDF library, or OCR) -> normalize -> chunk.
- Chunking policy: approximately 1,000 tokens (about 750 characters) with 200-token overlap.
- Store `document_id`, `chunk_index`, `char_range`, `source_url`, and `license` metadata.
- Asynchronously process ingestion: `POST /documents` places a job on a queue for a `BackgroundService`.

## 5. Embeddings

- MVP: `all-MiniLM-L6-v2`, chosen for speed and low cost; make the model configurable.
- Use batch processing, retry/backoff, and rate limiting.
- Cache embeddings in Redis by a content-and-model hash.

## 6. Vector search and RAG

- Flow: query -> embedding -> ANN top-k search -> retrieve chunks -> filter -> assemble context.
- Prompt template: system message, numbered snippets with citation headers, and the user query.
- Enforce a token budget: `model_max - safety_margin`.
- Return an LLM answer with citations containing chunk ranges and source URLs.

## 7. LLM integration

- Implement an adapter with timeout, retry, circuit breaker, and fallback behaviour.
- Sanitize prompts; optionally redact personally identifiable information.

## 8. Security

- Use JWT or API keys; use RBAC for administrative operations.
- Keep secrets in Vault or Azure Key Vault.
- Apply rate limits, TLS, and request-size limits.

## 9. Infrastructure

- Development: Docker Compose with PostgreSQL + pgvector, backend, and mock LLM.
- Production: Kubernetes/Helm, HPA, and GPU node affinity when needed.
- CI: GitHub Actions for builds, tests, migrations, and image publishing.

## 10. Observability

- Metrics: API, embedding, vector-search, and LLM latency; error rates; queue length.
- Tracing: OpenTelemetry and correlation IDs.
- Logs: structured Serilog logs, Prometheus, and Grafana.

## 11. Testing

- Unit tests: xUnit and Moq.
- Integration tests: Testcontainers for PostgreSQL + pgvector and a mock LLM.
- End-to-end tests: `/ask` scenarios with seeded data.
- Load tests: k6 or Locust targeting vector-search and LLM concurrency.

## 12. Data and backups

- Soft deletion, retention policy, and a PII-redaction pipeline.
- Regular PostgreSQL dumps and index snapshots.

## 13. Operational tasks

- Incremental reindexing on changes and scheduled full reindexing each night.
- A partial-reindex endpoint.
- Health checks for the database, a sample vector-index query, and an LLM ping.

## 14. MVP priorities

1. Authentication (API keys/JWT), `/ask`, and document CRUD.
2. Ingestion: URL/PDF -> chunks -> `all-MiniLM` embeddings -> storage.
3. Vector search and RAG with a mock LLM, minimizing reliance on external APIs.
4. Logging and basic metrics.
5. Reindex endpoint and background worker.

## Roadmap

Add prompt engineering, hallucination mitigation (fact checking), model A/B testing, automated backups, and advanced monitoring.

Optional deliverables: an OpenAPI specification, SQL DDL migrations, a Docker Compose development file, and a basic Helm chart.
