# 19. Durable RabbitMQ transport with PostgreSQL retry ledger

Date: 2026-05-30
Status: Accepted

## Context

Reminder delivery must survive transient provider failures, API restarts, and broker restarts. RabbitMQ is appropriate for durable command transport, but the business state of a reminder must not live only in the broker.

The system also must avoid automatic provider fallback. Switching provider after failure can violate patient communication preferences, provider contracts, billing expectations, and audit traceability.

## Decision

Use RabbitMQ through MassTransit for durable transport in production-like environments, and use PostgreSQL as the retry ledger and source of truth.

PostgreSQL stores:

- scheduled reminder status;
- attempt count;
- max attempts;
- retry base delay;
- retry max delay;
- last attempt timestamp;
- next attempt timestamp;
- last error code;
- provider message id;
- sent timestamp.

RabbitMQ transports `SendReminderCommand` and applies broker/consumer retry behavior. PostgreSQL decides whether work is due, retryable, sent, failed, or dead-lettered.

Provider fallback is not automatic. A failed send is retried against the same configured provider unless an operator changes configuration and intentionally requeues work.

## Consequences

- API restarts do not erase reminder intent.
- Broker queues do not become the only copy of business state.
- Retry and audit state are queryable for support and reporting.
- Multiple consumers can scale horizontally while PostgreSQL idempotency protects against duplicate sends.
- Operations must monitor both RabbitMQ queues and PostgreSQL retry/failure state.
