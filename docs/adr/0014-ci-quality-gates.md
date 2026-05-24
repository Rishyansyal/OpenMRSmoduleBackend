# 14. CI quality gates

Date: 2026-05-23

## Status

Accepted

## Context

Het project had geen consistente GitHub Actions checks voor backend, frontend en OpenMRS-module.

## Decision

Iedere repository krijgt een eigen workflow:

- Backend CI: restore, build, xUnit tests, Docker build en secret scan.
- Frontend CI: `npm ci`, lint, Vitest, Next build, Playwright smoke en secret scan.
- OpenMRS CI: Java 21/Maven tests voor de webhook-module, distro package check en secret scan.

## Consequences

- Pull requests falen vroeg op build-, test- of secretproblemen.
- CI gebruikt .NET 10 expliciet; lokale host-builds vereisen ook de .NET 10 SDK.
- De zwaardere OpenMRS runtime test is bewust niet verplicht op iedere PR.
