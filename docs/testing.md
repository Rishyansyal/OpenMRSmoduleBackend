# Testing

## Backend

```bash
dotnet restore OpenMRSmoduleBackend.csproj
dotnet restore OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj
dotnet test OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj
```

De backend testproject bevat unit tests en automatische integratietests. De integratietests gebruiken `WebApplicationFactory<Program>` met een tijdelijke SQLite databasefile. Daardoor starten ze de echte ASP.NET Core request pipeline, controllers, middleware, auth, EF repositories en Dapper repository zonder Docker Desktop of een lokale PostgreSQL-container nodig te hebben.

Unit tests:

- HMAC webhookvalidatie.
- Replay/stale timestamp rejection.
- AES-256-GCM encrypt/decrypt.
- Webhook idempotency.
- Reminderplanning en annulering.

Integratietests:

- `GET /health/db` controleert of de API de testdatabase bereikt via `IDbConnectionFactory`.
- `POST /api/webhooks/openmrs/appointments` accepteert een geldig gesigneerd appointment-event en schrijft webhook audit, appointment state en twee geplande reminders weg.
- Dubbele webhook event-id's geven `200 OK` terug en maken geen dubbele audit-, appointment- of reminderrecords.
- Ongeldige HMAC-signatures geven `401 Unauthorized` terug en schrijven niets naar de database.
- `POST /auth/register` geeft een geldig JWT terug dat toegang geeft tot `GET /api/reminders/scheduled`.

De testfixture zet tijdelijk deze configuratie:

- `Database__Provider=Sqlite`
- `Database__RunMigrations=false`
- `ConnectionStrings__DefaultConnection=Data Source=<temp-file>`
- testwaarden voor `Jwt`, `Security__EncryptionKey` en `Webhooks__OpenMrs__Secret`

Productie en Docker blijven standaard PostgreSQL gebruiken omdat `Database:Provider` zonder override naar `Postgres` valt.

### CI

`.github/workflows/backend-ci.yml` draait `dotnet test OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj` bij push en pull request. Daarmee draaien de unit tests en integratietests automatisch als required check.

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
