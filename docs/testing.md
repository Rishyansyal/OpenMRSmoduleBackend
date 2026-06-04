# Testing

## Backend

```bash
dotnet test OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj
```

The backend suite uses `WebApplicationFactory<Program>` and temporary SQLite databases for integration tests. Docker Desktop is not required for these checks.

Coverage includes:

- AES-256-GCM encryption.
- ASP.NET Identity login, JWT authorization, admin-only endpoints, and horizontal access isolation.
- Signed OpenMRS webhook validation, timestamp replay protection, unknown signatures, idempotency, scheduling, and cancellation.
- PostgreSQL-style reminder retry ledger behavior: retry waits and dead letters.
- Health endpoints and generated Swagger bearer configuration.

## OpenMRS Webhook Module

```bash
cd ../2.4-LU1-openMRS-Avans
mvn -pl openmrs-webhook-module test
```

The Java tests cover HMAC compatibility, property validation, and durable webhook outbox behavior.

## Runtime Acceptance

Docker Desktop is required for the full-stack acceptance pass:

```bash
cd ../2.4-LU1-openMRS-Avans
docker compose up -d --build

cd ../OpenMRSmoduleBackend
docker compose up -d --build
```

Verify OpenMRS O3 loads, appointment creation succeeds, signed webhooks reach the backend, RabbitMQ is healthy, and a simulated provider outage is retried after recovery. RabbitMQ is required outside `IntegrationTest`; local development no longer falls back to in-memory MassTransit.

OpenMRS O3 smoke check:

```powershell
cd ../2.4-LU1-openMRS-Avans
.\tests\smoke\openmrs-spa-smoke.ps1
```

The smoke check verifies `/openmrs` redirects to O3, `/openmrs/legacy` redirects to the legacy login page, SPA assets are reachable, runtime placeholders are resolved, and browser console errors are absent when Playwright is available.
