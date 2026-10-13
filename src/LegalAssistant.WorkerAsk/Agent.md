## Worker Ask Rules

- Worker-ask owns queued ask-job processing, RAG/LLM execution, and ask-event delivery.
- Worker-ask MUST NOT consume ingestion or embedding queues or dispatch ingestion/embedding messages.
- Worker MUST remain thin.
- Worker MUST NOT contain business logic.
- Worker MUST NOT directly access DbContext or external services.
- Worker MUST invoke Application use cases.
- Message deserialization belongs to the Worker/adapter boundary.
- Message transport concerns MUST NOT leak into Domain.
- Worker MUST propagate CancellationToken.
- Worker MUST handle message acknowledgement/retry/dead-letter concerns
  at the infrastructure/adapter boundary.
- Keep entrypoint files minimal.
- Do not place large startup routines in `Program.cs`.
- Put any route-like or startup-specific behavior into dedicated helper or endpoint files instead of the entrypoint.

## Shared Repository Rules

Follow the shared repository rules in `D:\repo\LegalAssistant\.github\copilot-instructions.md`.
