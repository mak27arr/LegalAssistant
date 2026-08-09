## Embeddings Service Rules

- Embeddings service is responsible only for embedding generation.
- It MUST NOT own document ingestion business logic.
- It MUST NOT directly manipulate application/domain persistence.
- Embedding provider implementations MUST be replaceable.
- Provider-specific APIs/models MUST remain inside the service boundary.
- Production embedding generation MUST be deterministic with respect to
  the configured model/version and input.
- Mock embedding generation MUST be used only for development/testing.
- Model name, dimensions and provider configuration MUST come from configuration.
- Embedding failures MUST be observable and retryable where appropriate.