# Test Report

Last updated: 2026-05-30

| Layer | Test set | Result |
|---|---|---|
| Backend | Unit and `WebApplicationFactory` integration tests | Passed locally: 35 tests |
| Swagger | Bearer security definition contract test | Passed locally |
| Retry ledger | Retry wait and dead-letter state transitions | Passed locally |
| Documentation | Mermaid source render through Kroki | PNG exports regenerated |
| OpenMRS webhook module | Maven/JUnit | Passed locally: 5 tests |
| OpenMRS distro package | Maven distro build and OMOD inspection | Passed locally |
| Compose configuration | Backend, hospital JSON overlay, and OpenMRS config parse | Passed locally with temporary secrets |
| Full Docker runtime | OpenMRS O3, RabbitMQ, backend, provider outage recovery | Blocked locally because Docker Desktop is not running |

## Commands Run

```bash
dotnet build OpenMRSmoduleBackend.csproj --no-restore
dotnet test OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj --no-restore
git diff --check

cd ../2.4-LU1-openMRS-Avans
mvn -pl openmrs-webhook-module test
mvn -P distro install -DskipTests
docker compose config --quiet
```

The full runtime acceptance pass remains required on a machine with Docker Desktop running.
