## Database Migration Rules

- DatabaseMigrations is a one-shot pre-deployment job that brings the shared LegalAssistant schema to the required version.
- DatabaseMigrations MUST complete successfully before API, worker, and Embeddings replicas start.
- It MUST NOT run as a long-lived service or own application message processing.
- It MUST use the existing Infrastructure DbContext and tracked migrations.
- It MUST be safe to rerun after a failed deployment.
- Do not place business logic, RabbitMQ consumers, or outbox dispatchers in this project.

## Shared Repository Rules

Follow the shared repository rules in `D:\repo\LegalAssistant\.github\copilot-instructions.md`.
