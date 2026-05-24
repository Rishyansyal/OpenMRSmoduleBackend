# 13. Pragmatic automated test strategy

Date: 2026-05-23

## Status

Accepted

## Context

De oplossing bestaat uit drie projecten. Full-stack tests met OpenMRS zijn waardevol, maar te traag en te kwetsbaar voor iedere pull request.

## Decision

We gebruiken een pragmatische testpiramide:

- Backend: xUnit unit tests voor HMAC, encryptie, webhook-idempotency en reminderplanning.
- Frontend: Vitest + Testing Library voor API-client, services en zichtbare reminderstatus.
- E2E: Playwright smoke tests tegen de frontend.
- OpenMRS module: Maven/JUnit tests voor signing en outbox-serialisatie.

Full OpenMRS containervalidatie blijft een lokale of handmatige acceptatietest.

## Consequences

- CI blijft snel genoeg voor PR's.
- Kritieke contracten worden automatisch gecontroleerd.
- Integratie met een echte OpenMRS instantie moet bij releases nog handmatig of in een zwaardere pipeline worden uitgevoerd.
