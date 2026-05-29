# 14. CI quality gates

Date: 2026-05-23

## Status

Accepted

## Context

Het project had geen consistente GitHub Actions checks voor de backend en de OpenMRS-module.

## Decision

Iedere repository krijgt een eigen workflow:

- Backend CI: restore, build, xUnit tests, Docker build en secret scan.
- OpenMRS CI: Java 21/Maven tests voor de webhook-module, distro package check en secret scan.

## Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **Geen CI, alleen handmatige reviews** | Build- en testfouten worden pas in main zichtbaar; secretleaks blijven onopgemerkt. Niet acceptabel voor een gedeelde repo. |
| **Azure DevOps Pipelines** | Werkt, maar vereist Azure-account en een aparte tool naast GitHub waar de code al leeft. Onnodige split-brain. |
| **Self-hosted Jenkins** | Eigen runners onderhouden, plugin-soep, beveiligingsupdates: te veel operationele last voor projectschaal. |
| **GitLab CI** | Vereist migratie naar GitLab; geen voordeel boven GitHub Actions voor onze workflow. |
| **Pre-commit hooks only (geen serverside CI)** | Lokaal te omzeilen; geeft geen garanties op een gedeelde branch. |
| **Heavy E2E op elke PR (full Docker stack)** | Te traag (minuten) en flaky; bewust uitgesloten ([ADR-0013](0013-pragmatic-automated-test-strategy.md)). |

## Consequences

- Pull requests falen vroeg op build-, test- of secretproblemen.
- CI gebruikt .NET 10 expliciet; lokale host-builds vereisen ook de .NET 10 SDK.
- De zwaardere OpenMRS runtime test is bewust niet verplicht op iedere PR.
