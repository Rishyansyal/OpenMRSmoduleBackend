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

### Multi-OpenMRS (multi-tenant) isolation

The backend serves multiple OpenMRS instances at once, each as its own organization. Tests guard the tenant boundary:

- **Per-organization webhook signatures** (`OpenMrsWebhookSignatureValidatorTests`):
  - each organization validates against its *own* webhook secret;
  - a payload signed with organization A's secret but claiming organization B is rejected (`INVALID_SIGNATURE`) — organization A cannot forge webhooks for B;
  - an unknown organization is rejected (`UNKNOWN_ORGANIZATION`).
- **HTTP session-cookie isolation** (`OpenMrsHttpClientCookieTests`): the shared `openmrs` `HttpClient` does not persist or resend session cookies between requests. Because multiple OpenMRS instances share a host (e.g. `host.docker.internal`, different port) and cookies ignore the port, a shared cookie jar would leak organization A's `JSESSIONID` to organization B and cause a `401`. The client is configured with `UseCookies = false` (Program.cs) and relies solely on per-request Basic auth. A control client with cookies enabled is asserted to *do* resend the cookie, so the test cannot pass falsely.

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
