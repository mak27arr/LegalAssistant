# RabbitMQ Architecture & Message Flow

**Date:** 2026-09-01  
**Project:** Legal Assistant  
**Document Purpose:** Complete specification and diagram of the RabbitMQ messaging pipeline and real-time SSE delivery architecture.

---

## 1. Overview & Topologies

Legal Assistant utilizes **RabbitMQ** for three main asynchronous workflow components:
1. **Document Ingestion Pipeline**: Handles document uploading, text extraction, normalization, and chunking.
2. **Embedding Pipeline**: Manages vector generation (`all-MiniLM-L6-v2`) and storage into PostgreSQL (`pgvector`).
3. **Ask Job Event Delivery**: Implements a Transactional Outbox pattern to deliver real-time job status updates via Server-Sent Events (SSE).

---

## 2. RabbitMQ Message Flow Diagram

```mermaid
flowchart TB
    subgraph Client ["Client (Browser / React Frontend)"]
        ReactApp["React UI"]
        EventSourceClient["EventSource (SSE)"]
    end

    subgraph APIHost ["API Host (ASP.NET Core Web API)"]
        DocController["POST /api/documents"]
        AskController["POST /api/ask/async"]
        SSEEndpoint["GET /api/ask/jobs/{jobId}/events"]
        OutboxDispatcher["AskJobOutboxDispatcherHostedService"]
        AskEventRelay["RabbitMqAskJobEventRelayHostedService"]
        SSEStreamService["AskJobEventStreamService (In-Memory Fanout)"]
    end

    subgraph Database ["Database (PostgreSQL + pgvector)"]
        PostgresDB[("PostgreSQL DB\n(documents, chunks, ask_jobs, message_outbox)")]
    end

    subgraph RabbitMQ ["RabbitMQ Message Broker"]
        subgraph IngestTopology ["Ingest Topology"]
            IngestQueue["Queue: ingest:jobs"]
        end

        subgraph EmbeddingTopology ["Embeddings Topology"]
            EmbedReqQueue["Queue: embeddings:requests"]
            EmbedCompQueue["Queue: embeddings:completed"]
        end

        subgraph AskJobTopology ["Ask Job Topology"]
            AskExchange["Exchange: ask:events (Topic)"]
            ApiRelayQueue["Exclusive Queue: ask:api-relay-*"]
        end
    end

    subgraph Workers ["Worker Services"]
        IngestWorker["RabbitMqIngestConsumerHostedService"]
        EmbeddingWorker["EmbeddingQueueWorker (LegalAssistant.Embeddings)"]
        EmbeddingCompletedWorker["RabbitMqEmbeddingCompletedConsumerHostedService"]
    end

    %% Ingestion & Embedding Pipeline
    ReactApp -->|"1. Upload Document"| DocController
    DocController -->|"2. Save Document Meta"| PostgresDB
    DocController -->|"3. Publish Ingest Job"| IngestQueue
    IngestQueue -->|"4. Consume Job"| IngestWorker
    IngestWorker -->|"5. Extract & Chunk Text"| PostgresDB
    IngestWorker -->|"6. Publish Embedding Task"| EmbedReqQueue
    EmbedReqQueue -->|"7. Consume Request"| EmbeddingWorker
    EmbeddingWorker -->|"8. Generate Vector (all-MiniLM)"| EmbeddingWorker
    EmbeddingWorker -->|"9. Publish Completed Vector"| EmbedCompQueue
    EmbedCompQueue -->|"10. Consume Vector"| EmbeddingCompletedWorker
    EmbeddingCompletedWorker -->|"11. Store Vector in pgvector"| PostgresDB

    %% Ask Realtime SSE Outbox Flow
    ReactApp -->|"A. Submit Ask Query"| AskController
    AskController -->|"B. Atomic DB Tx (ask_job + event + outbox)"| PostgresDB
    OutboxDispatcher -->|"C. Poll Pending Outbox Rows"| PostgresDB
    OutboxDispatcher -->|"D. Publish Event (routing: ask.job.id)"| AskExchange
    AskExchange -->|"E. Route to bound queue"| ApiRelayQueue
    ApiRelayQueue -->|"F. Consume & Relay Event"| AskEventRelay
    AskEventRelay -->|"G. Push to Fanout Stream"| SSEStreamService
    EventSourceClient <-->|"H. Stream Events (HttpOnly Cookie)"| SSEEndpoint
    SSEEndpoint <--> SSEStreamService
    SSEEndpoint -.->|"I. Replay Missed Events (Last-Event-ID)"| PostgresDB
```

---

## 3. Pipeline Breakdowns

### 3.1 Document Ingestion Flow
- **Endpoint**: `POST /api/documents`
- **Publisher**: `RabbitMqDocumentIngestJobPublisher`
- **Queue**: `ingest:jobs`
- **Consumer**: `RabbitMqIngestConsumerHostedService` in Worker project. Extracts document text, normalizes content, creates text chunks (~1000 tokens with 200 token overlap), and saves them to `document_chunks`.

### 3.2 Vector Embedding Flow
- **Publisher**: `RabbitMqEmbeddingRequestPublisher`
- **Request Queue**: `embeddings:requests`
- **Worker**: `EmbeddingQueueWorker` (`LegalAssistant.Embeddings` microservice). Computes dense embeddings using `all-MiniLM-L6-v2`.
- **Completion Queue**: `embeddings:completed`
- **Consumer**: `RabbitMqEmbeddingCompletedConsumerHostedService`. Persists generated vector arrays into the `embeddings` table (`pgvector`).

### 3.3 Real-Time SSE Outbox Flow
- **Endpoint**: `POST /api/ask/async`
- **Transactional Persistence**: Writes job state (`ask_jobs`), initial event (`ask_job_events`), and outbox entry (`message_outbox`) in a single DB transaction.
- **Outbox Dispatcher**: `AskJobOutboxDispatcherHostedService` reads pending outbox messages and publishes to RabbitMQ topic exchange `ask:events` with routing key `ask.job.<jobId>`.
- **Relay & SSE Stream**: `RabbitMqAskJobEventRelayHostedService` consumes from exchange via an exclusive API relay queue and feeds events to `AskJobEventStreamService`. Browser clients stream live status over `GET /api/ask/jobs/{jobId}/events` via `EventSource`.
