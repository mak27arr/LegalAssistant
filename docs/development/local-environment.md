## Local Development Environment

- Docker Compose is the canonical local infrastructure environment.
- Infrastructure dependencies MUST be started through Docker Compose.
- Use `docker/docker-compose.yml` as the production-safe base definition.
- Use `docker/docker-compose.dev.yml` for local ports, log mounts, ELK, and model bootstrap helpers.
- Do not require developers to install PostgreSQL, RabbitMQ, Ollama,
  or other infrastructure services directly on the host unless explicitly documented.
- Application services may run from the IDE/CLI against Docker infrastructure.
- Configuration MUST distinguish host execution from container execution.
- Secrets MUST live in `docker/.env` locally and MUST NOT be committed.
- Commit only `docker/.env.example`.
- Health checks MUST be defined for infrastructure dependencies where practical.
- Service startup order MUST NOT be relied upon as readiness;
  services MUST handle dependency readiness/failures explicitly.
