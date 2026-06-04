# OpenMRS Communication Module Backend

ASP.NET Core backend for the OpenMRS O3 communication module. The custom Next.js frontend has been removed from this architecture: users work through OpenMRS O3, direct REST/API clients, or Swagger during development.

The backend receives signed appointment events from OpenMRS, schedules 24-hour and 1-hour reminders, sends messages through FakeComWorld-compatible providers, and stores delivery state in PostgreSQL. RabbitMQ is required for reminder command transport outside integration tests; PostgreSQL remains the source of truth for appointment state, retry state, audit logs, and organization configuration.

## Current Scope

- Backend API only: ASP.NET Core controllers, JWT auth, OpenMRS integration, reminders, message sending, retention, metrics.
- OpenMRS O3 only: the OpenMRS distro provides the user-facing EMR experience.
- No custom Next.js frontend.
- Multi-OpenMRS support through organization-specific configuration.
- No automatic provider fallback. A reminder or ad-hoc message uses the explicitly selected/default provider for that organization; provider failure is retried, logged, and eventually failed/dead-lettered.

## Features

- Bootstrapped admin authentication, with public registration disabled unless explicitly enabled.
- JWT login and protected REST endpoints.
- Signed OpenMRS appointment webhook endpoint with HMAC validation, timestamp checks, and event idempotency.
- Optional OpenMRS poll worker per organization for environments where webhook delivery is unavailable.
- PostgreSQL persistence for users, appointment notifications, scheduled reminders, retry ledger fields, message logs, webhook logs, organization configs, provider configs, and templates.
- RabbitMQ transport for every non-test runtime, with MassTransit retry and dead-letter behavior.
- Provider adapters for SwiftSend, SecurePost, LegacyLink, and AsyncFlow.
- Health checks, OpenTelemetry tracing, and Prometheus metrics.

## Local System Overview

| System | Purpose | Local URL |
|---|---|---|
| OpenMRS O3 distro | EMR UI, OpenMRS backend, MariaDB | `http://localhost:3032/openmrs` |
| OpenMRSmoduleBackend | ASP.NET Core API and PostgreSQL | `http://localhost:5111` |
| RabbitMQ management | Local queue verification | `http://localhost:15672` |
| FakeComWorld | Simulated messaging providers | `http://localhost:1337` |

## Prerequisites

- Docker Desktop with Docker Compose v2.
- .NET 10 SDK for local development and tests outside Docker.
- Node.js only if regenerating C4 PNG diagrams through Mermaid CLI.
- OpenMRS O3 running on `http://localhost:3032`.
- FakeComWorld running on `http://localhost:1337`.

## Environment Setup

Create a local `.env` file:

```bash
cp .env.example .env
```

Minimum required values:

| Variable | Purpose |
|---|---|
| `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB` | PostgreSQL credentials and database name. |
| `JWT_SECRET` | JWT signing secret, at least 32 characters. |
| `SECURITY_ENCRYPTION_KEY`, `ENCRYPTION_KEY` | Base64 32-byte AES-GCM keys. |
| `OPENMRS_ORGANIZATION_ID`, `OPENMRS_BASE_URL`, `OPENMRS_USERNAME`, `OPENMRS_PASSWORD` | Legacy single-organization OpenMRS identity and connection used when JSON hospital config is absent. The id must match OpenMRS `OPENMRS_WEBHOOK_ORGANIZATION_ID`. |
| `OPENMRS_WEBHOOK_SECRET` | Legacy single-organization webhook secret. |
| `MESSAGING_STUDENT_GROUP`, `MESSAGING_*` | Provider credentials for FakeComWorld. |
| `RABBITMQ_HOST`, `RABBITMQ_USERNAME`, `RABBITMQ_PASSWORD` | Required for RabbitMQ transport outside `IntegrationTest`. |
| `ADMIN_EMAIL`, `ADMIN_PASSWORD` | Bootstrapped admin account. |
| `ALLOW_PUBLIC_REGISTRATION` | Set `true` only when public self-registration is intentionally allowed. |

Generate local 32-byte keys:

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
```

## Organization And Hospital Configuration

The current architecture supports multiple hospitals/OpenMRS instances. The preferred configuration shape is JSON under `HospitalConfiguration:Organizations`, where each organization defines:

- `OrganizationId`
- OpenMRS base URL and credentials
- webhook secret
- default provider
- timezone
- poller settings
- delivery retry settings
- enabled provider configs and encrypted credentials

At startup, the seeder stores organization rows in PostgreSQL:

- `organization_integration_configs`
- `organization_provider_configs`

When the JSON section is empty, the backend can seed a single legacy organization from the older `OpenMrs:*`, `Webhooks:OpenMrs:*`, and `Messaging:*` settings.

For a multi-hospital Docker deployment, create `hospital-config.json` from
`hospital-config.example.json` and mount it with the supplied overlay:

```bash
docker compose \
  -f docker-compose.yml \
  -f docker-compose.hospital-config.example.yml \
  up -d --build
```

## Run

From this backend folder:

```bash
docker compose up -d --build
```

Useful endpoints:

| Endpoint | URL |
|---|---|
| REST API | `http://localhost:5111` |
| Swagger UI | `http://localhost:5111/swagger` |
| Health check | `http://localhost:5111/health` |
| Prometheus metrics | `http://localhost:5111/metrics` |

Verify:

```bash
curl http://localhost:5111/health
```

Local runtime verification:

```powershell
..\2.4-LU1-openMRS-Avans\tests\smoke\openmrs-spa-smoke.ps1
```

Then confirm RabbitMQ is healthy at `http://localhost:15672`, trigger a signed synthetic webhook from Swagger or the demo endpoint, inspect `/api/reminders/scheduled`, and trigger `/api/reminders/trigger`. Scheduled reminder output is operational metadata only: it includes status, provider, timestamps, queue message id, provider message id, and a hashed encounter reference, not patient names, contact details, message content, or plaintext encounter ids.

## Auth Flow

The backend bootstraps an admin user when `Admin__Email` and `Admin__Password` are configured. Public registration returns `403 PUBLIC_REGISTRATION_DISABLED` unless `Admin__AllowPublicRegistration=true`.

Login:

```bash
curl -X POST http://localhost:5111/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.test","password":"<ADMIN_PASSWORD>"}'
```

## OpenMRS Appointment Webhook

OpenMRS posts appointment events to:

```text
POST /api/webhooks/openmrs/appointments
```

The backend validates `X-OpenMRS-Organization-Id`, timestamp, and HMAC signature before it processes the body. Accepted events upsert appointment state, write a webhook audit row, and create/cancel scheduled reminders. See [docs/webhook-openmrs-backend.md](docs/webhook-openmrs-backend.md).

## Reminder Message Templates

Default templates are stored in `message_templates` and can be updated through the admin reminder template endpoints. Supported placeholders are `{type}`, `{tijd}`, `{locatie}`, and `{instructies}`.

Synthetic examples:

- `24h`: `Herinnering: u heeft morgen een Controle op donderdag 4 juni om 10:30 bij Polikliniek A. Belangrijk: Neem uw medicatie mee. Neem contact op bij vragen.`
- `1h`: `Herinnering: u heeft over ongeveer 1 uur een Controle op donderdag 4 juni om 10:30 bij Polikliniek A.`

## Tests

```bash
dotnet test OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj
```

## Documentation

- [Architecture documentation](docs/c4/README.md)
- [ADR log](docs/adr/README.md)
- [Requirements](docs/requirements.md)
- [Security review](docs/security-review.md)
- [Authentication API](docs/auth-api.md)
- [Testing](docs/testing.md)
- [OpenMRS webhook integration](docs/webhook-openmrs-backend.md)
