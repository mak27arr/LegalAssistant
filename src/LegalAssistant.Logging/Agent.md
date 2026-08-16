## Logging Rules

- Logging module contains cross-cutting logging infrastructure only.
- Business decisions MUST NOT be implemented in the logging module.
- Do not log secrets, tokens, credentials or sensitive document content.
- Prefer structured logging.
- CorrelationId MUST be preserved across request/message boundaries.
- Logging MUST NOT become a dependency of Domain.
- Keep entrypoint files minimal when they exist.
- Do not place long startup routines in `Program.cs`.
- Put HTTP endpoint-like behavior into dedicated endpoint files instead of the entrypoint.
