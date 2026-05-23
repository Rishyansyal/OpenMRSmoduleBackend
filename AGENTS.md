# Agent Instructions — OpenMRS Module Backend

## Doel van de applicatie

ASP.NET Core 10 backend voor de OpenMRS Communicatiemodule. Stuurt berichten (SMS/e-mail/push) naar patiënten via externe providers, haalt patiëntgegevens op uit OpenMRS via FHIR R4, en logt alle berichtactiviteit voor audit.

---

## Architectuur

Layered mappenstructuur binnen één project (zie [ADR-0011](docs/adr/0011-layered-folder-structure.md)):

```
Api/             → Controllers, request/response records
Application/     → Interfaces + DTOs (geen framework-afhankelijkheden)
Domain/          → Entiteiten, value objects
Infrastructure/  → EF Core, HTTP clients, externe integraties
  Persistence/   → DbContext, migraties, connection factory
  Messaging/     → Provider-implementaties + MessagingService
  OpenMrs/       → FHIR R4 client
Program.cs       → Composition root: DI-registraties + middleware pipeline
```

**Dependency-richting:** `Api` → `Application` → `Domain`. `Infrastructure` implementeert interfaces uit `Application`.

**Nooit:** een controller die direct `Infrastructure` aanroept, of `Application` dat `Infrastructure` kent.

---

## Alvorens je code schrijft

1. **Lees de ADRs** in `docs/adr/` — beslissingen over ORM, auth, folder-structuur, secrets, etc. staan daar.
2. **Credentials gaan in `.env`**, nooit in `appsettings.json` of als default-waarde in een Options-klasse. Zie [ADR-0006](docs/adr/0006-secrets-via-env-file.md).
3. **Nieuwe entiteit = EF Core migratie.** Voeg `DbSet<T>` toe aan `ApplicationDbContext`, run `dotnet ef migrations add <Naam>`.

---

## Lokaal draaien

```bash
# 1. Kopieer en vul credentials in
cp .env.example .env

# 2. Start externe services
docker run -p 1337:8080 ghcr.io/avansict/in2.4-fakecomworld:main   # messaging providers
cd ../openmrs-distro-referenceapplication && docker compose up -d   # OpenMRS (poort 3032)

# 3. Start backend
docker compose up -d --build

# API:     http://localhost:5111
# Swagger: http://localhost:5111/swagger
```

> Gebruik altijd Docker Compose — `dotnet run` werkt niet zonder database-verbinding.

---

## Huidige endpoints

| Methode | URL | Beschrijving |
|---|---|---|
| POST | `/auth/register` | Registreer nieuwe gebruiker |
| POST | `/auth/login` | Login, geeft JWT terug |
| GET | `/auth/me` | Huidige ingelogde gebruiker |
| GET | `/api/messages/providers` | Beschikbare messaging providers |
| POST | `/api/messages` | Stuur bericht via provider |
| GET | `/api/messages/history` | Audit-log van verstuurde berichten |
| GET | `/api/messages/status/{id}` | AsyncFlow verwerkingsstatus |
| GET | `/api/openmrs/patients?q=` | Zoek patiënten via FHIR |
| GET | `/api/openmrs/patients/{id}` | Haal één patiënt op |
| GET | `/api/openmrs/appointments` | Haal encounters op uit OpenMRS |

Alle `/api/*` endpoints vereisen `Authorization: Bearer <jwt>`.

---

## Een nieuwe feature toevoegen

1. **Interface** in `Application/<Domein>/I<Feature>.cs`
2. **DTOs/records** in `Application/<Domein>/`
3. **Implementatie** in `Infrastructure/<Domein>/<Feature>.cs`
4. **Registreer** in `Program.cs`
5. **Controller** in `Api/Controllers/<Feature>Controller.cs`

---

## Naamgevingsconventies

- Namespaces volgen de mapnaam: `Api.Controllers`, `Application.Messaging`, `Infrastructure.OpenMrs`
- Interfaces: `I`-prefix (`IMessageProvider`, `IOpenMrsService`)
- Options-klassen: `<Naam>Options` in `Infrastructure`
- Database-kolommen: `snake_case` (zie bestaande EntityTypeConfiguration in `ApplicationDbContext`)
- Primary constructors waar mogelijk (C# 12), tenzij mutable velden nodig zijn

---

## Wat je NIET moet doen

- Geen credentials in `appsettings.json` of als default-waarde in Options-klassen
- Geen directe aanroepen van `Infrastructure` vanuit `Api`
- Geen `using var doc = JsonDocument.Parse(...)` + `yield return` — dit geeft `ObjectDisposedException`; gebruik `.ToList()` voor de `using`-block eindigt
- Geen `UseRouting()` expliciet aanroepen — `WebApplication` doet dit impliciet
- Geen Co-Authored-By in commit messages

---

## Messaging providers (FakeComWorld)

| Provider | Auth | Bijzonderheid |
|---|---|---|
| SwiftSend | X-API-KEY | Rate limit 10/min |
| SecurePost | JWT (3 min expiry) | Token-cache in `SecurePostProvider` (Singleton) |
| LegacyLink | HTTP Basic + XML | Alleen SMS, per ontvanger |
| AsyncFlow | X-API-KEY | Submit → poll status via trackingId |

`X-STUDENT-GROUP` header is verplicht voor alle providers.

---

## Beveiliging (vereisten uit opdrachtbeschrijving)

- TLS 1.3 voor transport (via reverse proxy in productie — HSTS guard in `Program.cs`)
- AES-256 voor opslag (nog te implementeren voor patiëntdata)
- Patiëntdata automatisch verwijderen na 14 dagen (nog te implementeren)
- Meta-informatie bewaren tot max 1 jaar zonder PII
