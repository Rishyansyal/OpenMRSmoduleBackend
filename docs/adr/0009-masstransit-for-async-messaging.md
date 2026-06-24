# 9. Use MassTransit for asynchronous messaging

Date: 2026-05-23
Status: Accepted, amended 2026-05-30 and 2026-06-03

## Context

Appointment reminders and retention work must not block HTTP requests. The webhook stores appointment/reminder intent; delivery happens later when a reminder is due.

The backend must support durable RabbitMQ transport in development, staging, and production so local testing validates the same queue behavior used after deployment.

## Decision

Use MassTransit as the .NET message bus abstraction.

- `ReminderWorker` claims due `scheduled_reminders` from PostgreSQL.
- It publishes `SendReminderCommand`.
- `SendReminderConsumer` sends through the configured provider and writes delivery/audit state.
- RabbitMQ is required outside `IntegrationTest`.
- In-memory transport is allowed only for automated integration tests.

PostgreSQL remains the durable business ledger. RabbitMQ transports commands; it does not own appointment, reminder, retry, or audit truth.

## Considered Alternatives

| Alternative | Why rejected |
|---|---|
| Raw `RabbitMQ.Client` | Would require custom retry, serialization, dead-letter, tracing, and idempotency wrappers. |
| Hangfire / Quartz | They couple scheduling and execution. The backend keeps schedule state in `scheduled_reminders` and only dispatches through the bus. |
| Plain `BackgroundService` queue | In-process queueing is lost on crash and does not scale across API instances. |
| Cloud-only broker | Adds vendor lock-in and blocks offline development. |

## Consequences

- HTTP requests stay fast.
- Consumers can scale horizontally.
- RabbitMQ provides durable transport and dead-letter behavior.
- PostgreSQL retry fields make support/audit queries possible.
- Every consumer must remain idempotent.
- Local development validates RabbitMQ-specific behavior before staging.
