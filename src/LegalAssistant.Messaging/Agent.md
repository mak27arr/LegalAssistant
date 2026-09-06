# Messaging Project Rules

- This project contains shared RabbitMQ transport infrastructure only.
- Do not reference Application, Domain, EF Core, documents, embeddings, or ask-job services.
- Keep business logic in service-specific adapters and handlers.
- Do not expose RabbitMQ channels or `RabbitMQ.Client` types to application handlers.
- Use one long-lived RabbitMQ connection per process.
- Use one channel per consumer. Serialize access to publisher channels because channels are not thread-safe.
- Never create a connection or channel per request or message.
- Declare exchanges, queues, bindings, DLX/DLQ, and retry infrastructure at endpoint startup.
- Publish persistent messages with message ID, correlation ID, message type, and content type.
- Use publisher confirms consistently.
- Consumers must use manual acknowledgements and ACK only after successful handling.
- Use bounded retries with exponential backoff. Never use unlimited `requeue: true`.
- Dead-letter messages after the retry limit or according to the explicit malformed-payload policy.
- Preserve correlation and retry headers when republishing messages.
- Handle connection loss, channel shutdown, cancellation, and reconnect consistently.
- Use structured logging with correlation IDs. Never log credentials, tokens, or sensitive payloads.
- Add deterministic unit tests for metadata, serialization, retry policy, topology, and ACK/NACK outcomes.
