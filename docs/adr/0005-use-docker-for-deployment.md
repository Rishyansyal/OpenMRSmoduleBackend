# 5. Use Docker for local development and deployment

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

The backend, PostgreSQL, and durable broker need a reproducible deployment
baseline that hospitals can configure without installing each dependency
manually.

## Decision

Use the multi-stage `Dockerfile` and `docker-compose.yml` with:

- `api`;
- `db` using PostgreSQL 17;
- `rabbitmq`.

Configuration is supplied through environment variables and a local `.env`.
Multi-hospital deployments mount JSON configuration by adding a volume for
`hospital-config.json` in a local compose override and setting
`HOSPITAL_CONFIG_FILE_PATH` in `.env`.

## Consequences

- Local and hosted deployments use the same container shape.
- PostgreSQL and RabbitMQ stay off public interfaces.
- Production TLS termination remains the responsibility of the deployment
  reverse proxy.
