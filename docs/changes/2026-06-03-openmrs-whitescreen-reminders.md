# OpenMRS White Screen And Reminder Delivery Changes

Date: 2026-06-03

## What Changed

- `/openmrs` now redirects to the OpenMRS O3 SPA home route.
- `/openmrs/legacy` now redirects to the legacy OpenMRS login page.
- The OpenMRS frontend Docker build pins the helper dependency `@react-aria/utils` so machines do not resolve a moving latest version.
- A new OpenMRS smoke script checks route redirects, SPA assets, unresolved runtime placeholders, and browser console errors when Playwright is available.
- RabbitMQ is now required outside `IntegrationTest`; development no longer falls back to in-memory MassTransit.
- Local Docker Compose exposes RabbitMQ management on `127.0.0.1:15672`.
- Scheduled reminders now store queue evidence: queue message id, queued timestamp, consumed timestamp, provider message id, attempts, status, and error code.
- Queue publish failures move reminders back to retry-wait instead of leaving them stuck as queued.
- Reminder admin responses now expose hashed encounter references instead of plaintext encounter ids.
- Reminder message rendering is extracted into a service with unit tests for the 24h and 1h default messages.
- Documentation and ADRs now reflect RabbitMQ-required development, strict provider selection, and privacy-safe operational evidence.

## Files Changed

OpenMRS distro:

- `gateway/default.conf.template`
- `gateway/default-ssl.conf.template`
- `frontend/Dockerfile`
- `tests/smoke/openmrs-spa-smoke.ps1`
- `README.md`

Backend:

- `Program.cs`
- `docker-compose.yml`
- `docker-compose.override.yml`
- `Domain/ScheduledReminder.cs`
- `Application/Reminders/*`
- `Infrastructure/Reminders/*`
- `Infrastructure/Messaging/Consumers/SendReminderConsumer.cs`
- `Infrastructure/Persistence/ApplicationDbContext.cs`
- `Infrastructure/Persistence/Migrations/20260603193637_AddReminderQueueEvidence.cs`
- `OpenMRSmoduleBackend.Tests/*`
- `README.md`
- `docs/*`
- `docs/adr/0021-require-rabbitmq-outside-integration-tests.md`
- `docs/adr/0022-privacy-safe-reminder-delivery-evidence.md`

## How To Verify

Run backend tests:

```bash
dotnet test OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj
```

Run the OpenMRS smoke check after Docker startup:

```powershell
cd ..\2.4-LU1-openMRS-Avans
.\tests\smoke\openmrs-spa-smoke.ps1
```

Runtime checks:

- Open `http://localhost:3032/openmrs`.
- Open `http://localhost:3032/openmrs/spa/home`.
- Open RabbitMQ management at `http://localhost:15672`.
- Trigger a signed synthetic appointment webhook.
- Confirm `/api/reminders/scheduled` shows two reminders, queue evidence, provider message id after delivery, and hashed encounter references.

## Known Limitations

- Full Docker/browser verification requires Docker Desktop to be running.
- The Java OpenMRS webhook module tests require Maven.
- Browser cache or an old service worker can still show stale behavior; clear site data for `localhost:3032` only after the smoke script passes.
- Operational APIs intentionally do not show patient contact details or message bodies.
