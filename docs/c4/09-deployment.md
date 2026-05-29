# Deployment Diagram — OpenMRS Communicatiemodule

Hoe het systeem fysiek wordt gedeployd: containers, netwerken, poorten en volumes. Gebaseerd op de drie `docker-compose.yml` bestanden.

## Lokale ontwikkelomgeving (Docker Compose)

```mermaid
flowchart TB
    classDef host fill:#f1f5f9,stroke:#475569,stroke-width:2px,color:#000
    classDef network fill:#fef9c3,stroke:#a16207,stroke-width:1px,color:#000
    classDef container fill:#dbeafe,stroke:#1e40af,stroke-width:1px,color:#000
    classDef db fill:#fce7f3,stroke:#9d174d,stroke-width:1px,color:#000
    classDef ext fill:#e0e7ff,stroke:#4338ca,stroke-width:1px,color:#000
    classDef volume fill:#dcfce7,stroke:#166534,stroke-width:1px,color:#000

    subgraph Host["💻 Developer Machine (Docker Desktop)"]
        Browser["🌐 Browser<br/>localhost:3032"]:::ext

        subgraph netBackend["backend network (bridge)"]
            API["📦 api<br/>ASP.NET Core 10<br/>:5111 → :8080<br/>read-only, no-new-privileges, cap_drop ALL"]:::container
            DB[("📦 db<br/>PostgreSQL 17<br/>internal only<br/>no port binding")]:::db
            VolPG[("💾 pgdata volume")]:::volume
        end

        subgraph netOMRS["openmrs network"]
            GW["📦 gateway<br/>:3032 → :80"]:::container
            OMFE["📦 openmrs-frontend"]:::container
            OMBE["📦 openmrs-backend"]:::container
            OMDB[("📦 mariadb 10.11")]:::db
            VolOMRS[("💾 openmrs-data")]:::volume
            VolDB[("💾 db-data")]:::volume
        end

        FAKE["📦 fakecomworld<br/>:1337 → :8080<br/>standalone container"]:::container
    end

    Browser -->|HTTPS in prod / HTTP dev| GW
    API -->|TCP 5432| DB
    DB --> VolPG
    API -.->|HTTPS :3032| GW
    GW --> OMFE
    GW --> OMBE
    OMBE --> OMDB
    OMBE --> VolOMRS
    OMDB --> VolDB
    API -.->|HTTPS :1337| FAKE
    OMBE -.->|signed webhook POST /api/webhooks/openmrs/appointments| API
```

## Container-overzicht

| Container | Image | Poorten | Volume | Security |
|---|---|---|---|---|
| `api` | `OpenMRSmoduleBackend` (custom build) | `127.0.0.1:5111 → 8080` | tmpfs `/tmp` | read_only, cap_drop ALL, no-new-privileges |
| `db` | `postgres:17-alpine` | — (internal only) | `pgdata` | no-new-privileges |
| `gateway` | `openmrs/openmrs-reference-application-3-gateway:qa` | `3032:80` | — | — |
| `openmrs-frontend` | `openmrs/openmrs-reference-application-3-frontend:qa` | — | — | — |
| `openmrs-backend` | `openmrs/openmrs-reference-application-3-backend:qa` | `5005:5005` (debug) | `openmrs-data` | — |
| `openmrs-db` | `mariadb:10.11.7` | — | `db-data` | — |
| `fakecomworld` | `ghcr.io/avansict/in2.4-fakecomworld:main` | `1337:8080` | — | — |

## Productie deployment (target architectuur)

```mermaid
flowchart TB
    classDef extuser fill:#fef3c7,stroke:#92400e,stroke-width:2px,color:#000
    classDef edge fill:#dbeafe,stroke:#1e40af,stroke-width:2px,color:#000
    classDef pod fill:#dcfce7,stroke:#166534,stroke-width:1px,color:#000
    classDef db fill:#fce7f3,stroke:#9d174d,stroke-width:1px,color:#000
    classDef ext fill:#e0e7ff,stroke:#4338ca,stroke-width:1px,color:#000

    Zorg["👤 Zorgmedewerker<br/>(browser)"]:::extuser

    subgraph DMZ["DMZ / Edge"]
        LB["🛡️ Reverse proxy<br/>TLS 1.3 terminatie<br/>HSTS + HTTPS-redirect"]:::edge
    end

    subgraph App["Applicatie-tier"]
        APIpod["📦 api (ASP.NET Core)<br/>:8080"]:::pod
        Bus["📦 RabbitMQ<br/>:5672"]:::pod
    end

    subgraph Data["Data-tier (privénetwerk)"]
        PG[("🗄️ PostgreSQL 17<br/>:5432<br/>backups, AES-at-rest")]:::db
    end

    subgraph Obs["Observability"]
        Prom["📊 Prometheus scrape<br/>/metrics"]:::pod
    end

    subgraph Ext["Externe systemen"]
        OMRS["💻 OpenMRS EMR<br/>FHIR R4 + webhooks"]:::ext
        Prov["💻 Messaging Providers<br/>SwiftSend / SecurePost / LegacyLink / AsyncFlow"]:::ext
    end

    Zorg -->|HTTPS :443| LB
    LB -->|HTTP :8080| APIpod
    OMRS -->|signed webhook :443| LB
    APIpod --> PG
    APIpod --> Bus
    Bus --> APIpod
    APIpod --> OMRS
    APIpod --> Prov
    APIpod --> Prom
```

## Security & netwerk-eisen

| Maatregel | Implementatie |
|---|---|
| **TLS 1.3** | Reverse proxy in productie. `app.UseHsts()` + `UseHttpsRedirection()` in Program.cs |
| **DB-isolatie** | PostgreSQL geen public port binding (`docker-compose.yml`). Alleen `backend`-netwerk |
| **API-isolatie** | API gebind op `127.0.0.1:5111` in dev (`API_PORT=127.0.0.1:5111`) |
| **Container hardening** | `read_only: true`, `cap_drop: ALL`, `no-new-privileges:true`, tmpfs `/tmp` |
| **Secrets** | `.env`-file, nooit in image of compose-defaults ([ADR-0006](../adr/0006-secrets-via-env-file.md)) |
| **CORS** | Whitelist via `ALLOWED_ORIGINS` (standaard: OpenMRS op `:3032`), geen wildcard |
| **Rate limiting** | Per-IP: 5/min auth, 10/min messaging, 100/min global |
| **AES-256-GCM** | PII bij rust in DB (encounter_id, patient data) |

## Opstartvolgorde

`start.sh` start de stack in deze volgorde:
1. **FakeComWorld** — standalone container (geen netwerkafhankelijkheid)
2. **OpenMRS distro** — gateway + openmrs-frontend + openmrs-backend + mariadb (~2 min)
3. **Backend + Postgres** — eigen compose, leest `.env`

Het script sourcet credentials uit `OpenMRSmoduleBackend/.env` zodat er één bron van waarheid is.
