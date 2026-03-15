# Детальний план імплементації

Мета: реалізувати RAG‑сервіс на основі `ASP.NET Core` з `Postgres + pgvector` і локальною/віддаленою LLM для юридичних запитів (MVP → production roadmap).

1) Архітектура компонентів
- Backend: `ASP.NET Core Web API` (сервіси через DI).
  - `DocumentService` — CRUD, кеш, парсинг.
  - `EmbeddingService` — генерація/батчинг embedding-ів.
  - `VectorDbLayer` — wrapper для pgvector/SQL, ANN search.
  - `RagPipelineService` — retrieve → augment → prompt build.
  - `LlmService` — адаптер для Ollama/GPT4All/провайдерів.
  - `AdminService` — reindex, metrics, feedback.

2) API ендпоінти (OpenAPI/swagger)
- `POST /ask` — body: `{ query, top_k?, filters? }` → response: `{ answer, sources[], score }`.
- `POST /documents` — додати документ (url/file).
- `GET/PUT/DELETE /documents/{id}` — метадані/контент (soft delete).
- `POST /admin/reindex` — async реіндексація.
- `GET /health` — DB, vector index, LLM.
- `POST /feedback` — human feedback.

3) DB schema (Postgres + pgvector)
- `documents`:
  - `id UUID PK`, `title text`, `url text`, `content text`, `metadata jsonb`, `created_at`, `updated_at`, `version int`, `is_deleted bool`.
- `document_chunks`:
  - `id UUID PK`, `document_id FK`, `chunk_index int`, `text text`, `char_range int4range`, `source_url text`, `created_at`.
- `embeddings`:
  - `id UUID PK`, `chunk_id FK`, `vector vector(<dim>)`, `model varchar`, `created_at`.

Індекси: pgvector HNSW/IVF, збереження `dim` у міграції і планом реіндексації при зміні моделі.

4) Ingest pipeline
- Підтримка: URL, PDF, HTML, raw text.
- Кроки: fetch → extract (Tika/PDF lib/ocr) → normalize → chunking.
- Chunking policy: ~1000 токенів (прибл. 750 chars) + overlap 200 токенів.
- Зберігати metadata: `document_id`, `chunk_index`, `char_range`, `source_url`, `license`.
- Асинхронно: POST /documents ставить job у чергу (BackgroundService).

5) Embeddings
- MVP: `all-MiniLM-L6-v2` (швидко, економно). Конфіг для заміни моделі.
- Batch processing, retry/backoff, rate-limit.
- Cache embeddings у Redis за хешем контенту+модель.

6) Vector search & RAG
- Flow: query → embedding → ANN search top_k → fetch chunks → filter → assemble context.
- Prompt template: system + numbered snippets з citation headers + user query.
- Token budget: enforce (model_max - safety_margin).
- Видача: LLM вертає answer і цитати (chunk ranges + source URLs).

7) LLM integration
- Адаптер із timeout, retry, circuit-breaker, fallback.
- Sanitize prompts (PII redaction опціонально).

8) Безпека
- Auth: JWT або API key, RBAC для admin.
- Secrets: Vault/Azure Key Vault.
- Rate limiting, TLS, request size limits.

9) Інфраструктура
- Dev: Docker Compose (Postgres+pgvector, backend, mock LLM).
- Prod: Kubernetes/Helm, HPA, nodeAffinity для GPU (за потреби).
- CI: GitHub Actions — build, tests, migrations, image push.

10) Observability
- Метрики: latency (API, embeddings, vector search, LLM), error rates, queue length.
- Tracing: OpenTelemetry, correlation IDs.
- Логи: Serilog структуровано, Prometheus + Grafana.

11) Тестування
- Unit: xUnit + Moq.
- Integration: testcontainers для Postgres+pgvector, mock LLM.
- E2E: сценарії для `/ask` з seed даними.
- Load tests: k6/Locust targeting vector search + LLM concurrency.

12) Дані та бекапи
- Soft delete + retention policy, PII redaction pipeline.
- Backups: регулярні дампи Postgres + знімки індексів.

13) Операційні задачі
- Reindex: incremental on change + scheduled full reindex (nightly).
- Partial reindex endpoint.
- Health checks: DB, sample query to vector index, LLM ping.

14) MVP пріоритети
1) Auth (API keys/JWT), `/ask`, `/documents` CRUD.
2) Ingest: URL/PDF → chunk → embeddings (all-MiniLM) → store.
3) Vector search + RAG з mock LLM (мінімізувати залежності від зовн. API).
4) Logging + базові метрики.
5) Reindex endpoint + background worker.

Roadmap: додати prompt engineering, hallucination mitigation (fact-check), A/B testing моделей, automated backups, advanced monitoring.

Можу додатково згенерувати (за потреби): OpenAPI spec, SQL DDL міграції, Docker Compose dev файл, базовий Helm chart.
