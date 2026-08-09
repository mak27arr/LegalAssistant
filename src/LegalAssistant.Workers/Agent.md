## Worker Rules

- Worker is an inbound/background adapter.
- Worker MUST remain thin.
- Worker MUST NOT contain business logic.
- Worker MUST NOT directly access DbContext or external services.
- Worker MUST invoke Application use cases.
- Message deserialization belongs to the Worker/adapter boundary.
- Message transport concerns MUST NOT leak into Domain.
- Worker MUST propagate CancellationToken.
- Worker MUST handle message acknowledgement/retry/dead-letter concerns
  at the infrastructure/adapter boundary.