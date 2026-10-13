## Core Rules

- Core contains stable cross-cutting abstractions shared by the LegalAssistant layers.
- Core MUST remain independent of application, domain, transport, and infrastructure concerns.
- Core MUST NOT depend on ASP.NET Core, EF Core, RabbitMQ, HTTP clients,
  file systems, configuration, or external services.
- Core types MUST be small, reusable, and focused on shared contracts or primitives.
- Core MUST NOT contain business workflows or persistence logic.
- Core MUST NOT perform I/O.
- Changes to Core MUST avoid introducing dependencies that create layer cycles.

## Shared Repository Rules

Follow the shared repository rules in `D:\repo\LegalAssistant\.github\copilot-instructions.md`.
