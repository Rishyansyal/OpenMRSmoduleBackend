# 9. Use MassTransit for asynchronous messaging and background services

Date: 2026-05-23

## Status

Accepted

## Context

Our application requires multiple background services that run on schedules or in response to events:

1. Hourly check: scan for appointments occurring within the next 24 hours → send reminders via MassTransit.
2. Daily check: identify and clean up appointments older than 14 days.
3. Annual cleanup: delete logs older than one year.

These are long-running, asynchronous operations that must not block the main HTTP request/response cycle. We need a message queue and service bus to coordinate them.

## Decision

We use **MassTransit** as our message bus and service orchestration layer.

- MassTransit is the standard service bus for ASP.NET Core; excellent C# integration, minimal boilerplate.
- Consumers (service handlers) subscribe to messages and process them asynchronously.
- Transport: RabbitMQ (production) or in-memory (local dev, see [ADR 0005](0005-use-docker-for-deployment.md) for Docker setup).
- Hosted services and scheduled jobs push messages; consumers pull and execute.

## Consequences

**Advantages:**
- Proven, mature ecosystem in the .NET world; widely used in enterprise.
- Decouples services: adding a new async job doesn't require modifying HTTP controllers.
- Built-in retry logic, dead-letter queues, and monitoring.
- Easy to scale: multiple instances can consume the same message queue.

**Disadvantages:**
- Adds a third service (message broker) to local dev and production. For dev, in-memory transport is acceptable; for prod, RabbitMQ or managed service (Azure Service Bus) is required.
- Team must understand pub/sub patterns and eventual consistency (not all data is instantly consistent across services).
- More complex failure scenarios: messages can be lost, delayed, or retried; must handle idempotency.

**Implementation notes:**
- Every consumer must be idempotent; processing the same message twice should be safe.
- Use correlation IDs to track a message through the system.
- Implement health checks for the message bus.
