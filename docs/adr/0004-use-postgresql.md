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

## Consequences

- Open source, geen licentiekosten, brede hosting-opties (managed bij elke major cloud).
- JSONB, full-text search, en degelijke transaction-semantics out-of-the-box.
- Team moet basis-SQL kennen van Postgres-dialect (verschilt licht van SQL Server).
- Lokale dev vereist Docker (of een lokale Postgres install). Acceptabel — Docker is hoe dan ook al onze dev-baseline (zie [ADR 0005](0005-use-docker-for-deployment.md)).
