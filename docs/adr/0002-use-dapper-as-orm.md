# 2. Use Dapper as ORM

Date: 2026-05-23

## Status

Accepted

## Context

We hebben een database access laag nodig voor onze domein-queries (patiëntdata, modules, etc.). De keuze gaat tussen een full ORM (EF Core) en een micro-ORM (Dapper).

EF Core wordt al gebruikt voor ASP.NET Core Identity (zie [ADR 0003](0003-use-identity-framework-for-auth.md)), dus die afhankelijkheid hebben we sowieso. De vraag is welke laag we kiezen voor onze eigen domein-queries.

## Decision

We gebruiken **Dapper** voor onze domein-queries. EF Core wordt enkel gebruikt voor Identity's eigen tabellen.

Connection-management gaat via een eigen `IDbConnectionFactory` zodat tests een fake connection kunnen injecteren en controllers geen connection strings kennen.

## Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **EF Core voor álle queries** | Lazy loading-risico's (N+1), change-tracker-overhead op write-heavy paden, verborgen SQL. Voor domein-queries waar we precies willen weten wat er over de wire gaat is dat te veel magie. |
| **Raw `ADO.NET` met `NpgsqlCommand`** | Geen mapping-helper; boilerplate-explosie. Dapper geeft de explicietheid van ADO.NET met 80% minder code. |
| **Andere micro-ORM (Massive, PetaPoco, RepoDB)** | Kleinere community en minder up-to-date met .NET 10 dan Dapper. Geen functionele winst. |
| **NHibernate** | Zwaargewicht; legacy reputatie; niet meer mainstream in .NET. |

## Consequences

- Queries zijn expliciet (raw SQL of parameterized) — geen verborgen N+1's door lazy loading.
- We schrijven mappings zelf (POCO ↔ resultset). Voor simpele rijen is dat triviaal; voor complexe object grafen meer werk dan EF.
- Twee patterns naast elkaar (EF voor Identity, Dapper voor de rest). Reviewers moeten alert zijn dat domein-code niet per ongeluk de `ApplicationDbContext` gaat misbruiken.
- Migraties voor onze eigen tabellen doen we via SQL scripts of een aparte tool (bv. DbUp / Flyway) — niet via EF migrations.
