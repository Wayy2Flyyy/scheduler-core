# scheduler-core

Production-grade distributed job scheduler built in C# (.NET 8). This repo ships a Coordinator API, Worker service, and shared contracts with Postgres-backed persistence, retries, leases, and observability.

## Features

- Coordinator + Worker architecture with Postgres persistence
- Immediate, delayed, and cron-like recurring jobs
- At-least-once execution with lease-based claims
- Idempotency keys on submission
- Retries with exponential backoff + dead-lettering
- Heartbeats for workers
- Structured JSON logging
- Health + metrics endpoints
- Minimal web UI for job visibility

## Repository Layout

```
src/Coordinator   ASP.NET Core API + dashboard
src/Worker        Hosted service worker
src/Shared        DTOs + utilities
migrations        SQL migrations
scripts           Helper scripts
samples           (reserved)
tests/Unit        Unit tests
tests/Integration Integration tests (Postgres)
```

## Prerequisites

- .NET SDK 8.x
- Docker + Docker Compose
- Postgres (if not using Docker)

## Quick Start (Docker)

```bash
cp .env.example .env
docker compose up --build
```

Once running:

- Coordinator API: http://localhost:5000
- Dashboard UI: http://localhost:5000
- Health: http://localhost:5000/health
- Metrics: http://localhost:5000/metrics

## Local Run (without Docker)

```bash
dotnet restore
./scripts/migrate.sh
dotnet run --project src/Coordinator
# In another terminal
DOTNET_ENVIRONMENT=Development dotnet run --project src/Worker
```

Update the connection string in `src/Coordinator/appsettings.json` or override via environment variables.

## API Usage

### Submit job

```bash
curl -X POST http://localhost:5000/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "type": "http_get",
    "payload": { "url": "https://example.com" },
    "runAt": "2025-01-01T00:00:00Z",
    "maxAttempts": 3,
    "timeoutSeconds": 30,
    "idempotencyKey": "example-http-1"
  }'
```

### Schedule recurring job

```bash
curl -X POST http://localhost:5000/api/jobs \
  -H "Content-Type: application/json" \
  -d '{
    "type": "cpu",
    "payload": { "durationSeconds": 2 },
    "cron": "*/5 * * * *",
    "idempotencyKey": "cpu-every-5min"
  }'
```

### Query job

```bash
curl http://localhost:5000/api/jobs?status=Running&take=25
curl http://localhost:5000/api/jobs/<jobId>
```

### Cancel or retry

```bash
curl -X POST http://localhost:5000/api/jobs/<jobId>/cancel
curl -X POST http://localhost:5000/api/jobs/<jobId>/retry -H "Content-Type: application/json" -d '{ "reason": "manual" }'
```

### Seed demo job

```bash
curl -X POST http://localhost:5000/api/seed
```

## Job Types

| Type | Payload | Description |
|------|---------|-------------|
| `http_get` | `{ "url": "https://example.com" }` | HTTP GET and capture status |
| `cpu` | `{ "durationSeconds": 2 }` | Busy-loop CPU workload |
| `file_write` | `{ "fileName": "hello.txt", "content": "hello" }` | Write file on worker disk |

## Reliability Model (Leases, Retries, Timeouts)

- Workers claim jobs via `/api/workers/claim` and receive a lease for a fixed duration.
- Workers renew leases while executing; the coordinator requeues when leases expire.
- Every claim creates a `job_runs` record and increments `attempts`.
- Each failure schedules an exponential backoff retry; after `maxAttempts` the job moves to `DeadLetter`.
- Jobs are executed at-least-once; ensure handlers are idempotent where possible.

## Scaling Workers

Use Docker Compose scaling:

```bash
docker compose up --build --scale worker=3
```

Each worker gets its own ID and claims jobs independently.

## Common Commands

```bash
make restore
make build
make test
make format
make migrate
make up
make down
```

## Tests

```bash
dotnet test tests/Unit/Coordinator.Tests/Coordinator.UnitTests.csproj
dotnet test tests/Unit/Worker.Tests/Worker.UnitTests.csproj
dotnet test tests/Integration/Coordinator.IntegrationTests/Coordinator.IntegrationTests.csproj
```

## Troubleshooting

- **Migrations failing**: ensure Postgres is reachable and credentials match `.env`.
- **No jobs running**: check worker logs and ensure the worker can reach the coordinator URL.
- **Jobs stuck in Running**: lease monitor will requeue after lease expiration.

## CI

GitHub Actions runs build, test, and format verification on each push/PR.

---

## License

MIT
