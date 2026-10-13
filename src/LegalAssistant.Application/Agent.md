## Application Rules

- Application contains the LegalAssistant use cases and application orchestration.
- Each use case MUST represent one meaningful business/application operation.
- Application MUST NOT contain transport-specific concerns.
- Application MUST NOT directly access databases, file systems,
  HTTP clients, RabbitMQ, or other infrastructure implementations.
- Application MAY define abstractions required by its use cases.
- Application MUST NOT depend on Infrastructure.
- Application coordinates Domain logic; it does not replace Domain logic.
- Application DTOs MUST represent use-case contracts, not persistence models.
- Application services MUST NOT become generic "god services".
- Prefer explicit dependencies over service-locator patterns.
- CancellationToken MUST be propagated through asynchronous operations.

## Shared Repository Rules

Follow the shared repository rules in `D:\repo\LegalAssistant\.github\copilot-instructions.md`.
