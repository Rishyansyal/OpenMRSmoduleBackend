# 13. Pragmatic automated test strategy

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

Full OpenMRS container tests are valuable but slow. Most backend regressions can
be caught faster without starting the full stack.

## Decision

- Backend: xUnit unit tests and `WebApplicationFactory` integration tests using
  temporary SQLite databases.
- OpenMRS webhook OMOD: Maven/JUnit tests for signing, configuration, and outbox
  behavior.
- Release acceptance: run the OpenMRS O3, RabbitMQ, backend, and provider outage
  recovery workflow with Docker.

## Consequences

- Pull-request feedback stays fast.
- Critical contracts receive automated coverage.
- Docker runtime acceptance is still required before production rollout.
