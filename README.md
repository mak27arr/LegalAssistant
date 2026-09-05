Getting started

- Run `docker compose -f docker-compose.yml -f docker-compose.dev.yml up` from `/docker` to start the local development stack.
- Copy `docker/.env.example` to `docker/.env` and keep local secrets there. Commit only `docker/.env.example`.
- Open solution in Visual Studio and run the API project. By default it uses in-memory DB if no connection string provided.
- Frontend development UI is available through the `frontend` container at `http://localhost:3000`.
- The React frontend uses the existing backend API only and proxies `/api` requests to the `api` service inside Docker.
- Browser auth uses a secure HttpOnly BFF session cookie plus `X-CSRF-TOKEN` on unsafe API requests; JavaScript does not receive access or refresh tokens.
- Async ask events stream from `/api/ask/jobs/{jobId}/events` with native `EventSource`, authenticated ownership checks, durable replay, and heartbeats.
