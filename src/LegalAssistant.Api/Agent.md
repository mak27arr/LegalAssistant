## API Rules

- API is an inbound adapter.
- Controllers/endpoints MUST remain thin.
- API MUST NOT contain business logic.
- API MUST NOT access DbContext or repositories directly.
- API MUST NOT publish messages directly.
- API MUST invoke Application use cases.
- Request/response DTOs belong to API.
- API DTOs MUST NOT be reused as Domain models.
- API-specific validation belongs in API when it concerns transport format.
- Business validation belongs in Application/Domain.
- HTTP status codes and transport-specific concerns belong to API.
- Middleware MUST handle cross-cutting HTTP concerns only.
- Keep `Program.cs` minimal.
- Do not place endpoint bodies or long route handlers in `Program.cs`.
- Put endpoints in separate files under `ServiceEndpoints/` or `Endpoints/` and expose them through small extension methods.
