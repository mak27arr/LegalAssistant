## Domain Rules

- Domain contains the LegalAssistant business concepts, rules, and invariants.
- Domain entities MUST protect their own invariants.
- Domain MUST have no dependency on ASP.NET Core, EF Core, RabbitMQ,
  HTTP clients, file system, configuration, logging infrastructure,
  or other external infrastructure.
- Domain methods SHOULD express business intent rather than technical operations.
- Domain services SHOULD be used only for business logic that does not
  naturally belong to a single entity or value object.
- Domain MUST NOT orchestrate application workflows.
- Domain MUST NOT perform I/O.
- Domain MUST NOT depend on Application.
- Domain models MUST NOT contain DTO concerns.
- Avoid anemic domain models when business invariants belong to the model.

## Shared Repository Rules

Follow the shared repository rules in `D:\repo\LegalAssistant\.github\copilot-instructions.md`.
