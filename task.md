# Legal Assistant - Architecture and MVP Requirements

## 1. Project architecture

Backend: ASP.NET Core Web API.

Services:

- **Document Service** - manages the database of laws and source links.
- **Embedding Service** - generates embeddings for documents.
- **Vector DB Layer** - integrates with PostgreSQL and pgvector.
- **RAG Pipeline Service** - retrieves information, augments the prompt, and generates an answer.
- **LLM Service** - integrates with a local model through Ollama, GPT4All, or an API.

API endpoints:

- `/ask` - accepts a user question and returns an answer with its sources.
- `/documents` - CRUD operations for documents and links.
- `/health` - checks system health.

## 2. Database (PostgreSQL + pgvector)

`Documents` table:

- `Id` (PK)
- `Title` - name of the law or code
- `Url` - link to an official government resource
- `Content` - law text, when cached
- `Embedding` - pgvector vector
- `Metadata` - type, for example law, court decision, or commentary

Indexes:

- HNSW or IVFFlat for fast embedding search.

## 3. User-query workflow

1. The user submits a question to `/ask`.
2. The Embedding Service generates an embedding for the question.
3. The Vector DB Layer finds the closest documents.
4. The Document Service loads the text from cache or from an official government resource.
5. The RAG Pipeline Service builds a prompt from the question and retrieved documents.
6. The LLM Service generates an answer.
7. The answer is returned with links to its sources.

## 4. Local AI model

- Use Ollama or GPT4All as a server for a local model.
- Suggested model: Mistral 7B or LLaMA 2 13B, balancing performance and quality.
- Integrate through REST API or gRPC.
- Generate embeddings separately with sentence-transformers or `all-MiniLM-L6-v2`.

## 5. Infrastructure

- Docker for containerization: PostgreSQL + pgvector, backend, and LLM.
- CI/CD: GitHub Actions or Azure DevOps.
- Logging: Serilog and Kibana.
- Monitoring: Prometheus and Grafana.
- Testing: xUnit plus integration tests for the RAG pipeline.

## 6. MVP functionality

- A question produces an answer with citations.
- CRUD operations for documents and links.
- Cache frequently used laws.
- Log requests and answers.
