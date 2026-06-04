# 9. Use MassTransit for asynchronous messaging

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

Appointment reminders and retention work must not block HTTP requests. The webhook stores appointment/reminder intent; delivery happens later when a reminder is due.

The backend must support durable RabbitMQ transport in production-like environments while still allowing lightweight local development.

## Decision

Use MassTransit as the .NET message bus abstraction.

- `ReminderWorker` claims due `scheduled_reminders` from PostgreSQL.
- It publishes `SendReminderCommand`.
- `SendReminderConsumer` sends through the configured provider and writes delivery/audit state.
- RabbitMQ is used when `RabbitMq:Host` is configured.
- In-memory transport is allowed only for local development and tests.

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
- Local in-memory transport does not validate RabbitMQ-specific failure behavior; staging smoke tests must use RabbitMQ.
