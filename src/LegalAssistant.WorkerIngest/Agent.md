## Worker Ingest Rules

- Worker-ingest owns document ingestion, embedding-request delivery, embedding-completion handling, and stale-ingest recovery.
- Worker-ingest MUST NOT process ask jobs or dispatch ask-event messages.
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
