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