# OpenMRS Communication Module Backend

ASP.NET Core 10 backend for the OpenMRS communication module. It handles authentication, patient lookup through OpenMRS, appointment reminder scheduling, signed OpenMRS webhooks, message sending, message history, and provider integration through FakeComWorld.

## Features

- JWT-based register/login flow.
- REST API with Swagger UI.
- PostgreSQL persistence.
- OpenMRS FHIR/patient integration.
- Signed OpenMRS appointment webhook endpoint.
- Optional OpenMRS poll worker for reminder scheduling.
- Messaging provider adapters for SwiftSend, SecurePost, LegacyLink, and AsyncFlow.
- Health checks and Prometheus metrics.

## Local System Overview

This backend is one of three repositories in the local workspace:

| Repo | Purpose | Local URL |
|---|---|---|
| `2.4-LU1-openMRS-Avans` | OpenMRS EMR and MariaDB | http://localhost:3032/openmrs |
| `OpenMRSmoduleBackend` | ASP.NET Core API and PostgreSQL | http://localhost:5111 |

FakeComWorld runs separately on http://localhost:1337 and simulates the external messaging providers.

## Prerequisites

- Docker Desktop with Docker Compose v2.
- .NET 10 SDK for local development and tests outside Docker.
- OpenMRS running on http://localhost:3032.
- FakeComWorld running on http://localhost:1337.

## Environment Setup

Create a local `.env` file:

```bash
cp .env.example .env
```

Fill in all required values. At minimum, local Docker startup requires:

| Variable | Purpose |
|---|---|
| `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` | PostgreSQL credentials and database name. |
| `JWT_SECRET` | JWT signing secret. Use at least 32 characters. |
| `SECURITY_ENCRYPTION_KEY` | Base64 32-byte encryption key. |
| `ENCRYPTION_KEY` | Base64 32-byte AES-GCM key. |
| `OPENMRS_USERNAME`, `OPENMRS_PASSWORD` | OpenMRS credentials used by the backend. |
| `OPENMRS_WEBHOOK_SECRET` | HMAC secret shared with the OpenMRS webhook module. |
| `MESSAGING_STUDENT_GROUP` | FakeComWorld student group. |
| `MESSAGING_*` provider credentials | FakeComWorld provider credentials. |

Generate local keys:

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
```

Or with OpenSSL:

```bash
openssl rand -base64 32
```

FakeComWorld credentials can be viewed at http://localhost:1337 after the provider container is running.

## Run The Full Stack

From the parent workspace folder (`2.4/`):

Linux/macOS/Git Bash:

```bash
./start.sh
```

The script starts FakeComWorld, OpenMRS, and this backend.

## Run Only The Backend

Start dependencies first:

```bash
# FakeComWorld providers
docker run -d --name fakecomworld -p 1337:8080 ghcr.io/avansict/in2.4-fakecomworld:main
# If it already exists:
docker start fakecomworld

# OpenMRS
cd ../2.4-LU1-openMRS-Avans
docker compose up -d
```

Then start the backend:

```bash
cd ../OpenMRSmoduleBackend
docker compose up -d --build
```

| Service | URL |
|---|---|
| REST API | http://localhost:5111 |
| Swagger UI | http://localhost:5111/swagger |
| Health check | http://localhost:5111/health |
| Prometheus metrics | http://localhost:5111/metrics |

Verify:

```bash
curl http://localhost:5111/health
```

Expected response:

```json
{"status":"Healthy"}
```

## Local Development Without Docker API Container

Use Docker for PostgreSQL or provide your own connection string, then run:

```bash
dotnet restore
dotnet run
```

The default local launch profile uses http://localhost:5111.

## Example API Flow

Register:

```bash
curl -X POST http://localhost:5111/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.test","password":"<LOCAL_TEST_PASSWORD>"}'
```

Login:

```bash
curl -X POST http://localhost:5111/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"user@example.test","password":"<LOCAL_TEST_PASSWORD>"}'
```

Send a test message:

```bash
curl -X POST http://localhost:5111/api/messages \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "provider": "swiftsend",
    "type": "SMS",
    "recipients": ["+31612345678"],
    "content": "Test message from the communication module."
  }'
```

## OpenMRS Appointment Webhook

OpenMRS posts appointment events to:

```text
POST /api/webhooks/openmrs/appointments
```

The endpoint validates HMAC-SHA256 headers, deduplicates events, and creates scheduled 24-hour and 1-hour reminders. See [`docs/webhook-openmrs-backend.md`](docs/webhook-openmrs-backend.md).

Important shared secrets:

- `OPENMRS_WEBHOOK_SECRET`
- OpenMRS global property `openmrswebhook.secret`

## Tests

```bash
dotnet test OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj
```

The test suite includes unit tests and integration tests using `WebApplicationFactory` with a temporary SQLite database. This allows auth, webhook, health, and reminder endpoints to run in CI without local Docker dependencies.

## Useful Docker Commands

```bash
# Start backend and PostgreSQL
docker compose up -d --build

# Show status
docker compose ps

# Follow API logs
docker compose logs -f api

# Stop backend and database
docker compose down

# Stop and remove PostgreSQL volume
docker compose down -v
```

To stop the whole local system:

```bash
cd ../OpenMRSmoduleBackend && docker compose down
cd ../2.4-LU1-openMRS-Avans && docker compose down
docker stop fakecomworld
```

## Documentation

- [Architecture documentation](docs/c4/README.md)
- [ADR log](docs/adr/README.md)
- [Requirements](docs/requirements.md)
- [Security review](docs/security-review.md)
- [Testing](docs/testing.md)
- [OpenMRS webhook integration](docs/webhook-openmrs-backend.md)
