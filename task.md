1. Архітектура проєкту
Backend: ASP.NET Core (Web API).

Сервіси:

Document Service — робота з базою законів/посилань.

Embedding Service — генерація embeddings для документів.

Vector DB Layer — інтеграція з PostgreSQL + pgvector.

RAG Pipeline Service — retrieval + augmentation + генерація відповіді.

LLM Service — інтеграція з локальною моделлю (через Ollama, GPT4All або API).

API endpoints:

/ask — приймає питання користувача, повертає відповідь з джерелами.

/documents — CRUD для документів/посилань.

/health — перевірка стану системи.

2. База даних (Postgres + pgvector)
Таблиця Documents:

Id (PK)

Title (назва закону/кодексу)

Url (посилання на державний ресурс)

Content (текст закону, якщо кешується)

Embedding (vector, pgvector)

Metadata (тип: закон, судове рішення, коментар)

Індекси:

HNSW або IVFFlat для швидкого пошуку по embeddings.

3. Workflow запиту користувача
Користувач надсилає питання → /ask.

Embedding Service генерує embedding для питання.

Vector DB Layer шукає найближчі документи.

Document Service підтягує текст (з кешу або з державного ресурсу).

RAG Pipeline Service формує prompt: питання + витягнуті документи.

LLM Service (локальна модель) генерує відповідь.

Відповідь повертається з посиланнями на джерела.

4. Локальна AI-модель
Використати Ollama або GPT4All як сервер для локальної моделі.

Модель: Mistral 7B або LLaMA 2 13B (баланс продуктивності та якості).

Інтеграція через REST API або gRPC.

Embeddings можна генерувати окремо (sentence-transformers або all-MiniLM-L6-v2).

5. Інфраструктура
Docker для контейнеризації (Postgres + pgvector, backend, LLM).

CI/CD: GitHub Actions або Azure DevOps.

Логування: Serilog + Kibana.

Моніторинг: Prometheus + Grafana.

Тестування: xUnit + інтеграційні тести для RAG pipeline.

6. MVP-функціонал
Запит → відповідь з цитатами.

CRUD для документів/посилань.

Кешування найчастіше використовуваних законів.

Логування запитів і відповідей.