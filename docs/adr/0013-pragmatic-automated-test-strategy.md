# 13. Pragmatic automated test strategy

Date: 2026-05-23

## Status

Accepted

## Context

De oplossing bestaat uit drie projecten. Full-stack tests met OpenMRS zijn waardevol, maar te traag en te kwetsbaar voor iedere pull request.

## Decision

We gebruiken een pragmatische testpiramide:

- Backend: xUnit unit tests voor HMAC, encryptie, webhook-idempotency en reminderplanning.
- OpenMRS module: Maven/JUnit tests voor signing en outbox-serialisatie.

Full OpenMRS containervalidatie blijft een lokale of handmatige acceptatietest.

## Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **Volledige integratiesuite (echte OpenMRS-container) op elke PR** | Onbetrouwbaar (flaky), traag (5–10 min opstart) en verspilling van CI-tijd voor wijzigingen die OpenMRS niet raken. Acceptatie blijft handmatig of in een nightly-build. |
| **Alleen unit tests, geen integratietests** | Mist het type bug dat juist *tussen* lagen ontstaat (DI-misregistratie, middleware-volgorde, EF-mappings). De `WebApplicationFactory` + SQLite-aanpak in [testing.md](../testing.md) dekt dat goedkoop. |
| **BDD met SpecFlow / Reqnroll** | Krachtig voor business-owned scenarios, maar voor ons team-grootte introduceert het een tweede taal (Gherkin) en een onderhoudslast. xUnit met duidelijke testnamen volstaat. |
| **Contract testing (Pact) tussen componenten** | Waardevol voor losgekoppelde teams; voor onze projectschaal introduceert het een onderhoudslast zonder duidelijk voordeel. |
| **Manual QA only** | Niet schaalbaar; regressies worden te laat ontdekt; voldoet niet aan het rubric. |

## Consequences

- CI blijft snel genoeg voor PR's.
- Kritieke contracten worden automatisch gecontroleerd.
- Integratie met een echte OpenMRS instantie moet bij releases nog handmatig of in een zwaardere pipeline worden uitgevoerd.
