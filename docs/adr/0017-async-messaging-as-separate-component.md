# 17. Asynchronous messaging as a separate component

Date: 2026-05-25
Status: Accepted, amended 2026-05-30

## Context

The backend has work that is not tied to one HTTP request:

- scheduled reminders 24h and 1h before an appointment;
- provider calls that can be slow, rate-limited, or temporarily unavailable;
- retention jobs;
- retry and dead-letter handling.

## Decision

Treat asynchronous messaging as a separate logical component with its own failure domain and scale axis.

| Part | Implementation | Role |
|---|---|---|
| Producer | `ReminderWorker` | Claims due PostgreSQL reminders and publishes commands. |
| Transport | RabbitMQ through MassTransit | Durable command delivery in development, staging, and production. |
| Consumer | `SendReminderConsumer` | Performs FHIR lookup, sends through the selected provider, and records results. |
| Ledger | PostgreSQL `scheduled_reminders` and logs | Owns business state, retries, and audit. |

The worker and consumer currently run inside the API process, but their contract and transport boundary allow separate scaling later.

## Consequences

- Provider latency and retries do not leak into the webhook response.
- RabbitMQ can buffer work during spikes.
- PostgreSQL prevents duplicate reminder intent and records retry/failure state.
- Dead-lettered work is operationally visible.
- Provider fallback is deliberately not part of this component. The component retries the configured provider and records failure when exhausted.

## Related ADRs

- [ADR-0009](0009-masstransit-for-async-messaging.md)
- [ADR-0019](0019-durable-rabbitmq-postgresql-retry-ledger.md)
