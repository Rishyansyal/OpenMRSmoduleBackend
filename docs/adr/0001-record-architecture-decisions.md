# 1. Record architecture decisions

Date: 2026-05-23

## Status

Accepted

## Context

Tijdens dit project nemen we keuzes die de structuur, toolingsstack en operationele kant van de backend bepalen. Zonder log raakt het *waarom* achter een keuze snel kwijt — vooral als teamleden later instromen of switchen.

## Decision

We loggen elke significante architectuurkeuze als een Architecture Decision Record (ADR) in [docs/adr/](.). Format: Context → Decision → Consequences. Nummering oplopend, status één van `Proposed`, `Accepted`, `Deprecated`, `Superseded`.

## Consequences

- Nieuwe teamleden kunnen `docs/adr/` lezen en het *waarom* terugvinden zonder iemand te storen.
- Beslissingen worden niet stilzwijgend teruggedraaid; een wijziging vereist een nieuwe ADR die de oude superseden.
- Kleine kosten: tijd om ADR te schrijven (~10 min) bij elke significante keuze.
