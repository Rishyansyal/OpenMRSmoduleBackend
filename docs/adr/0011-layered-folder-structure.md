# 11. Use a layered folder structure within a single project

Date: 2026-05-23

## Status

Accepted

## Context

De backend begint klein (één controller) maar groeit naar OpenMRS-integraties, webhooks ([ADR 0008](0008-webhook-for-openmrs-integration.md)), async messaging ([ADR 0009](0009-masstransit-for-async-messaging.md)) en straks meer domein-logica. Zonder structuur eindigt dat in een Controllers/Data-soep waarin business logic in controllers belandt en database-code zich verspreidt.

Tegelijk willen we de overhead van meerdere `.csproj` projecten (Clean Architecture-stijl) vermijden zolang de codebase nog klein is.

## Decision

We hanteren een **layered mappenstructuur binnen één project**:

```
Api/             # presentation: controllers, DTOs, request/response models
Application/     # use cases / services, interfaces voor infrastructure
Domain/          # entities, value objects, domeinregels — geen framework-deps
Infrastructure/  # implementaties: Dapper repos, EF DbContext, Identity, externe integraties
  Persistence/
```

**Dependency-richting** (top → bottom):

- `Api` → `Application` → `Domain`
- `Infrastructure` → `Application`, `Domain` (implementeert interfaces uit Application)
- `Program.cs` (composition root) kent alle lagen en wired DI

**Namespaces** volgen de mapnaam zonder project-prefix (`Api.Controllers`, `Infrastructure.Persistence`, etc.). Zie [ADR 0012](0012-namespace-convention.md) als die er is.

## Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **Clean Architecture met aparte `.csproj`'s per laag** | Dwingt dependency-richting af via compiler; ideaal voor grote codebases. Voor onze huidige omvang puur overhead: vier projecten om te restoreen, te builden en te referencen waar één voldoet. Path naar splitsing blijft open — alleen project references toevoegen. |
| **Vertical Slice Architecture** | Eén map per use case (`SendMessage/`, `RegisterUser/`, …). Sterk voor teams die parallel aan features werken; voor ons kleine team verspreidt het juist gerelateerde code. We hergebruiken `Infrastructure` veel — een verticale slice zou veel duplicatie geven. |
| **Modular Monolith (één project per module + intern layered)** | Tussenoplossing tussen Clean Architecture en wat wij doen. Te zwaar voor de huidige scope; potentieel waardevol als de module uitbreidt naar meerdere bounded contexts. |
| **Flat / Controllers + Data folders zonder lagen** | Werkt voor één controller; bij groei eindigt business logic in controllers en queries verspreid over de codebase. Precies wat we willen voorkomen. |

## Consequences

- Lagen zijn zichtbaar in de mappenboom; reviewers kunnen makkelijk zien of een controller direct in `Infrastructure` reikt (smell) of via `Application` (ok).
- Dependency-richting wordt **niet** afgedwongen door de compiler (zoals bij aparte `.csproj`'s wel het geval is). Discipline + code review houden het schoon.
- Wanneer de codebase groot wordt of teams gaan splitsen, kunnen we per laag een `.csproj` afsplitsen zonder de code zelf te herschrijven — alleen project references toevoegen.
- `Application/` en `Domain/` zijn nu nog leeg (`.gitkeep`); de structuur staat klaar voor de eerste use case / entity.
