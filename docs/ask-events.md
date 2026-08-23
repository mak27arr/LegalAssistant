# Ask Job Events

This document defines the event contract used for async `ask` processing, RabbitMQ topic routing, durable event log replay, outbox dispatch, and SSE delivery.

## Goals

- Keep `ask` status delivery realtime without polling the database every second.
- Allow reconnect and failover across API instances using `Last-Event-ID`.
- Keep the database as the source of truth.
- Avoid dual-write risk by storing the ask event log and outbox row in the same DB transaction.

## Event Model

Each ask job status transition produces one durable event record.
That event is also written to the outbox in the same transaction, then a background dispatcher publishes it to RabbitMQ.

### Fields

- `eventId`: monotonically increasing `long` used as the SSE `id`.
- `jobId`: `Guid` of the ask job.
- `status`: one of `Queued`, `InProgress`, `Completed`, `Failed`.
- `actorScopeKey`: scope used for idempotency and routing.
- `idempotencyKey`: request idempotency key.
- `conversationId`: optional conversation identifier.
- `error`: optional failure message.
- `occurredAtUtc`: UTC timestamp when the transition was recorded.

### Topic routing keys

- `ask.job.queued`
- `ask.job.inprogress`
- `ask.job.completed`
- `ask.job.failed`

### SSE format

- `id:` is `eventId`
- `event:` is the status-specific event name
- `data:` is the JSON payload

## Replay and Failover

- The API SSE endpoint must accept `Last-Event-ID`.
- On reconnect, the API replays all events with `eventId > Last-Event-ID`.
- If the connection drops or an API instance fails, the client reconnects and resumes from the durable event log.

## Delivery Flow

1. API or worker writes `ask_jobs`, `ask_job_events`, and the outbox row in one DB transaction.
2. `AskJobOutboxDispatcherHostedService` claims pending ask outbox rows and publishes them to RabbitMQ topic exchange `ask:events`.
3. `RabbitMqAskJobEventRelayHostedService` in the API host relays RabbitMQ messages into the local SSE fanout.
4. `AskJobEventStreamService` serves live SSE and replays missed events from `ask_job_events` on reconnect.

## Source of Truth

- Job state remains in `ask_jobs`.
- Event history remains in `ask_job_events`.
- Outbox delivery state remains in `message_outbox`.
- RabbitMQ is the delivery bus, not the source of truth.
