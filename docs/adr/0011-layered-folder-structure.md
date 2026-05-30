# 11. Use a layered folder structure within one project

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

The backend contains HTTP endpoints, application contracts, domain entities,
database repositories, workers, and external integrations. It needs visible
boundaries without the overhead of multiple project files.

## Decision

Use these folders:

```text
Api/             controllers and middleware
Application/     use-case contracts and DTOs
Domain/          persisted entities
Infrastructure/  EF Core, Identity, workers, OpenMRS, and provider adapters
Program.cs       composition root
```

Namespaces follow folder names, for example `Api.Controllers` and
`Infrastructure.Persistence`.

## Consequences

- Boundaries stay easy to inspect in code review.
- The compiler does not enforce all dependency directions.
- The layers can be split into separate projects later if the backend grows.
