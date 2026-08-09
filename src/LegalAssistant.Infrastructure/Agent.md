## Infrastructure Rules

- Infrastructure contains implementations of Application abstractions.
- Infrastructure is the only layer allowed to perform external I/O.
- EF Core and DbContext MUST remain inside Infrastructure.
- RabbitMQ.Client MUST remain inside Infrastructure.
- HTTP clients for external services MUST remain inside Infrastructure.
- File-system access MUST remain inside Infrastructure.
- Infrastructure MUST NOT contain business rules that belong to Domain.
- Infrastructure services MUST be replaceable through Application abstractions.
- Infrastructure MUST NOT expose vendor-specific types to Application
  unless explicitly required by the abstraction.
- Database mappings/configurations MUST remain in Infrastructure.
- External-service configuration MUST remain in Infrastructure.