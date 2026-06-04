# 21. Require RabbitMQ outside integration tests

Date: 2026-06-03
Status: Accepted

## Context

Reminder delivery must be validated through the same broker behavior during local development and production-like runs. In-memory MassTransit can hide queue connectivity, publish failure, routing, retry, and dead-letter behavior.

## Decision

Require `RabbitMq:Host`, `RabbitMq:Username`, and `RabbitMq:Password` outside the `IntegrationTest` environment. `IntegrationTest` may use MassTransit in-memory transport to keep automated tests isolated and fast.

## Consequences

- Development startup fails fast when RabbitMQ is not configured.
- Docker Compose local development uses RabbitMQ by default.
- Queue failures are visible before deployment.
- Tests that do not need broker behavior remain lightweight.
