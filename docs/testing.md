# Testing

## Backend

```bash
dotnet restore OpenMRSmoduleBackend.csproj
dotnet restore OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj
dotnet test OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj
```

Gedekt:

- HMAC webhookvalidatie.
- Replay/stale timestamp rejection.
- AES-256-GCM encrypt/decrypt.
- Webhook idempotency.
- Reminderplanning en annulering.

## Frontend

```bash
cd ../openMRSmoduleFrontend
npm ci
npm run lint
npm run test
npm run build
npm run e2e
```

Gedekt:

- API-client auth/error behavior.
- Reminder service.
- Herinneringenpagina.
- Playwright smoke test voor loginroute.

## OpenMRS webhookmodule

```bash
cd ../2.4-LU1-openMRS-Avans
mvn -pl openmrs-webhook-module test
```

Gedekt:

- HMAC-signature compatibiliteit met backend.
- Outbox entry serialisatie.
