# Test Report

Laatste update: 2026-05-23

| Laag | Testset | Status |
|---|---|---|
| Backend unit | HMAC, encryptie, webhook scheduling | Lokaal geslaagd: 8 tests |
| Backend integration | EF/DB migratie via CI Postgres service | Workflow toegevoegd; migratie lokaal ontdekt met `dotnet ef migrations list --no-build` |
| Frontend unit | Vitest + Testing Library | Lokaal geslaagd: 4 tests |
| Frontend e2e | Playwright smoke | Lokaal geslaagd: 1 Chromium smoke test |
| OpenMRS module | Maven/JUnit | Tests toegevoegd; lokaal geblokkeerd door ontbrekende Maven-installatie |
| Full OpenMRS runtime | Handmatige acceptatietest | Niet standaard in CI |

## Bekende verificatiebeperking

Docker Desktop draait niet en Maven is niet geinstalleerd. Docker build, Docker Compose startup en OpenMRS Maven tests zijn daarom lokaal nog niet uitgevoerd. De CI-configuratie gebruikt expliciet .NET 10 en Java 21.

## Lokaal uitgevoerde commands

- `dotnet restore .\OpenMRSmoduleBackend.Tests\OpenMRSmoduleBackend.Tests.csproj`
- `dotnet build .\OpenMRSmoduleBackend.Tests\OpenMRSmoduleBackend.Tests.csproj --no-restore`
- `dotnet test .\OpenMRSmoduleBackend.Tests\OpenMRSmoduleBackend.Tests.csproj --no-build`
- `dotnet tool restore`
- `dotnet ef migrations list --no-build`
- `npm run test`
- `npm run lint`
- `npm run build`
- `npx playwright install chromium`
- `npm run e2e`
