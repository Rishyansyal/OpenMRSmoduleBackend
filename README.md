# OpenMRS Communicatiemodule — Backend

ASP.NET Core 10 backend voor het versturen van berichten en afspraakherinneringen via externe messaging providers.

---

## Snel opstarten (alle diensten tegelijk)

Vanuit de bovenliggende map (`2.4/`):

```bash
../start.ps1
# of op Linux/macOS:
../start.sh
```

---

## Opstarten (stap voor stap)

### 1. Vereisten

- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (draait PostgreSQL + de API zelf)
- .NET 10 SDK wanneer je zonder Docker wilt builden/testen
- OpenMRS draaiend op poort `3032` (zie `openmrs-distro-referenceapplication`)
- FakeComWorld providers draaiend op poort `1337`

### 2. FakeComWorld starten

```bash
# Eerste keer
docker run -d --name fakecomworld -p 1337:8080 ghcr.io/avansict/in2.4-fakecomworld:main

# Daarna
docker start fakecomworld
```

### 3. OpenMRS starten

```bash
cd ../openmrs-distro-referenceapplication
docker compose up -d
```

> OpenMRS is beschikbaar op `http://localhost:3032` — het opstarten duurt ~2 minuten.

### 4. Omgevingsvariabelen instellen

```bash
cp .env.example .env
```

Open `.env` en vul de waarden in. De FakeComWorld API keys zijn te vinden op `http://localhost:1337`.

### 5. Backend starten

```bash
docker compose up -d --build
```

| Dienst | URL |
|---|---|
| REST API | http://localhost:5111 |
| Swagger UI | http://localhost:5111/swagger |
| Prometheus metrics | http://localhost:5111/metrics |

### 6. Verificatie

```bash
curl http://localhost:5111/health
```

Verwacht: `{"status":"Healthy"}`

---

## Voorbeeld: eerste aanvraag

```bash
# 1. Registreer een gebruiker
curl -X POST http://localhost:5111/auth/register \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Admin123!"}'

# 2. Login en kopieer het token
curl -X POST http://localhost:5111/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"admin@example.com","password":"Admin123!"}'

# 3. Stuur een testbericht (vervang <TOKEN> door het JWT uit stap 2)
curl -X POST http://localhost:5111/api/messages \
  -H "Authorization: Bearer <TOKEN>" \
  -H "Content-Type: application/json" \
  -d '{
    "provider": "swiftsend",
    "type": "SMS",
    "recipients": ["+31612345678"],
    "content": "Testbericht vanuit de communicatiemodule."
  }'
```

## OpenMRS webhook

OpenMRS stuurt afspraak-events naar:

```text
POST /api/webhooks/openmrs/appointments
```

De webhook gebruikt HMAC-SHA256 headers en maakt geplande 24u/1u reminders aan. Zie [webhookdocumentatie](docs/webhook-openmrs-backend.md).

Belangrijke secrets in `.env`:

- `SECURITY_ENCRYPTION_KEY`
- `OPENMRS_WEBHOOK_SECRET`

## Tests

```bash
dotnet test OpenMRSmoduleBackend.Tests/OpenMRSmoduleBackend.Tests.csproj
```

Dit draait unit tests en automatische integratietests. De integratietests starten de echte ASP.NET Core pipeline met `WebApplicationFactory` en een tijdelijke SQLite database, zodat webhook-, auth-, health- en reminder-endpoints ook in GitHub Actions zonder lokale Docker dependency getest worden.

---

## Alles stoppen

```bash
docker compose down
docker stop fakecomworld
```

---

## Meer informatie

- [Architectuurdocumentatie (C4)](docs/c4/README.md)
- [ADR-logboek](docs/adr/)
- [Requirements](docs/requirements.md)
- [Security review](docs/security-review.md)
- [Testing](docs/testing.md)
- [Agent-instructies](AGENTS.md)
