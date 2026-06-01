# Agent Instructions - OpenMRS Communication Backend

## Purpose

ASP.NET Core 10 backend for OpenMRS O3 appointment reminders and direct messaging. The custom Next.js frontend has been removed. OpenMRS O3 remains the user-facing EMR.

## Architecture

```text
Api/             Controllers and middleware
Application/     Interfaces and DTOs
Domain/          Persisted entities
Infrastructure/  EF Core, Identity, RabbitMQ/MassTransit, OpenMRS FHIR, providers
Program.cs       Composition root
```

Read `docs/adr/` before changing architecture. New persisted entities require an EF Core migration.

## Configuration

- Secrets belong in `.env`, environment variables, or a mounted hospital JSON config.
- Use `hospital-config.example.json` as the multi-OpenMRS template.
- Each organization has its own OpenMRS credentials, webhook secret, provider config, and retry policy.
- RabbitMQ is required outside local `Development` and `IntegrationTest`.

## Auth

- ASP.NET Core Identity stores users and roles.
- Startup bootstraps an `Admin` user from configuration.
- Public `/auth/register` is disabled unless intentionally enabled.
- Admin-only endpoints include reminder trigger/templates/dead-letter retry, data retention trigger, and demo webhook signing.

## Reliability

- RabbitMQ transports reminder commands.
- PostgreSQL `scheduled_reminders` remains the authoritative retry ledger.
- Retryable failures return to `retry_wait` with exponential backoff.
- Permanent failures and exhausted retries remain visible as `failed_permanent` or `dead_lettered`.
- Do not add automatic provider fallback unless the product requirement changes.

## Verification

```bash
dotnet build OpenMRSmoduleBackend.csproj
dotnet test OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj
```

For full runtime acceptance, start the OpenMRS distro and this backend with Docker Compose, then verify OpenMRS O3, RabbitMQ readiness, webhook delivery, and provider outage recovery.
