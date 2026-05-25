# 8. Use webhooks for OpenMRS integration, not polling APIs

Date: 2026-05-23

## Status

Accepted

## Context

Our system needs to receive appointment data from OpenMRS (Electronic Health Record system) into our backend. Two primary integration patterns are possible:

1. **Polling API**: Backend polls OpenMRS at regular intervals to fetch new or updated appointments.
2. **Webhooks**: OpenMRS pushes appointment events to our backend when they occur.

## Decision

We use **webhooks** for all OpenMRS → backend communication.

- OpenMRS sends a POST request to our backend's webhook endpoint (e.g., `POST /api/webhooks/appointments`) whenever an appointment is created or modified.
- Our webhook controller receives and validates the payload, then passes it to the service layer for encryption and storage.
- No continuous polling; data flows as events occur.

## Considered alternatives

| Alternative | Why rejected |
|---|---|
| **Polling the OpenMRS REST/FHIR API** | High load on both sides (we keep asking, OpenMRS keeps answering); latency between event and reminder; complex bookkeeping for "what did we already see?" Wastes resources on idle periods. |
| **Server-Sent Events (SSE) from OpenMRS** | Long-lived HTTP connection; not natively supported by OpenMRS modules; brittle through corporate firewalls and reverse proxies. |
| **Shared message queue (OpenMRS publishes to RabbitMQ / Kafka, we consume)** | Adds operational complexity for OpenMRS deployers and us. Webhooks are simpler and OpenMRS-friendly; we already have a message bus internally ([ADR-0009](0009-masstransit-for-async-messaging.md)) for our own async work. |
| **Database-level integration (read OpenMRS DB directly)** | Tight coupling to OpenMRS schema; security nightmare; explicitly discouraged by OpenMRS. |
| **File drop / scheduled export** | Not real-time; reminders would be late or miss same-day appointments. |

## Consequences

**Advantages:**
- Lower server load: we don't poll repeatedly; we only process events when they arrive.
- Real-time: appointments are recorded immediately upon creation in OpenMRS.
- Simpler to reason about: clear event-driven architecture.

**Disadvantages:**
- Requires OpenMRS to support webhooks (or a middleware that translates). If OpenMRS lacks webhooks, we must implement polling as a fallback.
- Webhook endpoints must be publicly reachable (or via a VPN/secure tunnel) from OpenMRS deployment.
- We must handle duplicate/replay events: idempotency is essential.

**Implementation notes:**
- Validate webhook signatures (HMAC) if OpenMRS supports it; prevents spoofing.
- Log all incoming webhook payloads for audit and debugging.
- Implement idempotency keys or deduplication logic in the service layer.