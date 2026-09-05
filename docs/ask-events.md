# Ask Job Events

This document defines the event contract used for async `ask` processing, RabbitMQ topic routing, durable event log replay, outbox dispatch, and authenticated SSE delivery.

## Authentication

Browser clients authenticate with the application session cookie named `__Host-legalassistant.session`.
The cookie is `HttpOnly`, `Secure`, `SameSite=Lax`, path-scoped to `/`, and contains only an opaque session key. The server stores the session ticket in `auth_sessions`, and Data Protection keys are shared through `data_protection_keys` so multiple API instances can read the cookie.

State-changing API requests must send the antiforgery request token in the `X-CSRF-TOKEN` header. Fetch the token with:

```http
GET /api/auth/csrf
```

Safe `GET` requests, including the SSE endpoint, do not require a CSRF token.

## Ownership

Each new ask job is owned by the authenticated local user through `ask_jobs.owner_user_id`.
The API derives ownership only from the authenticated `NameIdentifier` claim. Browser requests must not send `X-Actor-Key`.

The following endpoints require an authenticated application session and enforce ownership:

- `POST /api/ask/async`
- `GET /api/ask/jobs/{jobId}`
- `GET /api/ask/jobs/{jobId}/events`

Missing jobs and jobs owned by another user both return `404`.

## Event Model

Each ask job status transition produces one durable event record.
That event is also written to the outbox in the same transaction, then a background dispatcher publishes it to RabbitMQ.

Browser-facing payload fields:

- `jobId`: `Guid` of the ask job.
- `status`: one of `Queued`, `InProgress`, `Completed`, `Failed`.
- `question`: original ask text.
- `topK`: retrieval count used by the job.
- `conversationId`: optional conversation identifier.
- `error`: optional failure message.
- `result`: completed answer payload when available.
- `createdAt`: job creation timestamp.
- `updatedAt`: latest event/update timestamp.

Internal event rows still keep idempotency and routing details, but the browser API does not expose actor scope or idempotency keys.

## SSE Format

Create streams with native `EventSource` on the same origin:

```ts
export function createAskEventStream(jobId: string) {
  return new EventSource(`/api/ask/jobs/${jobId}/events`, {
    withCredentials: true
  });
}
```

The API writes:

- `retry: 5000` when the stream opens.
- `id:` as the durable event id.
- `event:` as the lower-case status event name.
- `data:` as the JSON payload.
- `: keep-alive` heartbeat comments about every 20 seconds.

Streams are bounded to about 10 minutes. Native `EventSource` reconnects automatically and sends `Last-Event-ID`; the API replays durable events with ids greater than that value.

## Delivery Flow

1. API or worker writes `ask_jobs`, `ask_job_events`, and the outbox row in one DB transaction.
2. `AskJobOutboxDispatcherHostedService` claims pending ask outbox rows and publishes them to RabbitMQ topic exchange `ask:events`.
3. `RabbitMqAskJobEventRelayHostedService` in the API host relays RabbitMQ messages into the local bounded SSE fanout.
4. `AskJobEventStreamService` authorizes job ownership, replays missed events from `ask_job_events`, sends heartbeats, and streams live fanout events.

## Deployment Notes

Expose React and the API through one HTTPS origin:

```text
https://legal-assistant.example/
https://legal-assistant.example/api/*
```

Production proxies must disable buffering and caching for `text/event-stream`, forward the original scheme and host, keep timeouts longer than the SSE lifetime, and avoid caching authenticated API responses.

## Source of Truth

- Job state remains in `ask_jobs`.
- Event history remains in `ask_job_events`.
- Session state remains in `auth_sessions`.
- Shared cookie keys remain in `data_protection_keys`.
- Outbox delivery state remains in `message_outbox`.
- RabbitMQ is the delivery bus, not the source of truth.
