## Local Development Environment

- Docker Compose is the canonical local infrastructure environment.
- Infrastructure dependencies MUST be started through docker-compose.
- Do not require developers to install PostgreSQL, RabbitMQ, Ollama,
  or other infrastructure services directly on the host unless explicitly documented.
- Application services may run from the IDE/CLI against Docker infrastructure.
- Configuration MUST distinguish host execution from container execution.
- Secrets MUST NOT be committed to docker-compose files.
- Health checks MUST be defined for infrastructure dependencies where practical.
- Service startup order MUST NOT be relied upon as readiness;
  services MUST handle dependency readiness/failures explicitly.