# 4. Use PostgreSQL as primary database

Date: 2026-05-23

## Status

Accepted

## Context

We hebben een relationele database nodig die zowel ASP.NET Core Identity ([ADR 0003](0003-use-identity-framework-for-auth.md)) als onze Dapper-queries ([ADR 0002](0002-use-dapper-as-orm.md)) ondersteunt. De realistische opties: PostgreSQL, SQL Server, MySQL/MariaDB, SQLite.

## Decision

We gebruiken **PostgreSQL 17** voor zowel lokaal (Docker) als productie.

- Provider: `Npgsql.EntityFrameworkCore.PostgreSQL` voor Identity / EF Core.
- Driver: `Npgsql` voor Dapper.
- Lokaal: via `docker-compose` service `db` met een named volume `pgdata`.

## Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **SQL Server** | Licentiekosten in productie; sterk Windows-leaning; minder neutrale keuze voor SaaS dat overal moet kunnen draaien. |
| **MySQL / MariaDB** | Werkt, maar minder rijke feature-set (zwakkere JSONB, geen `RETURNING`-clausule in oudere versies, beperkter window-functions). Voor de queries die we doen geen meerwaarde. |
| **SQLite** | Geen concurrent writes onder load; geen geschikte productie-DB voor een SaaS met meerdere API-instanties. We gebruiken het wel als test-DB voor integratietests ([ADR-0013](0013-pragmatic-automated-test-strategy.md)). |
| **MongoDB / NoSQL** | Onze data is sterk relationeel (FK's tussen `appointment_notifications` ↔ `scheduled_reminders`, transacties bij webhook-verwerking). NoSQL kost ACID en wint niets. |
| **CockroachDB / YugabyteDB** | Horizontaal-schaalbare relationals, maar overkill voor onze schaal en met operationele complexiteit die we niet aankunnen. |

## Consequences

- Open source, geen licentiekosten, brede hosting-opties (managed bij elke major cloud).
- JSONB, full-text search, en degelijke transaction-semantics out-of-the-box.
- Team moet basis-SQL kennen van Postgres-dialect (verschilt licht van SQL Server).
- Lokale dev vereist Docker (of een lokale Postgres install). Acceptabel — Docker is hoe dan ook al onze dev-baseline (zie [ADR 0005](0005-use-docker-for-deployment.md)).
