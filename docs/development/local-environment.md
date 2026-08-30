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

## Local Authentication Setup

- Google OAuth credentials for local development MUST be stored in `docker/.env`.
- The backend auth configuration is injected through `docker/docker-compose.dev.yml`.
- The current local backend base URL is `http://localhost:5000`.
- The current local frontend URL is `http://localhost:3000`.
- The Google OAuth redirect URI registered in Google Cloud for local development must be `http://localhost:5000/signin-google`.
- The frontend sign-in entry point is discovered from `GET /api/auth/config`; it must not hardcode the Google login URL.
- Access tokens should stay in memory in the frontend runtime.
- Refresh tokens should be issued by the backend and stored in an `HttpOnly` cookie.
