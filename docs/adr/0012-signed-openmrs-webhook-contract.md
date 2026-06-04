# 12. Signed OpenMRS webhook contract

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

OpenMRS appointment changes must reach the backend quickly and securely. The
backend supports multiple OpenMRS hospital deployments.

## Decision

OpenMRS posts events to `POST /api/webhooks/openmrs/appointments` with:

- `X-OpenMRS-Event-Id`;
- `X-OpenMRS-Event-Type`;
- `X-OpenMRS-Timestamp`;
- `X-OpenMRS-Organization-Id`;
- `X-OpenMRS-Signature: sha256=<hex>`.

The signature is HMAC-SHA256 over `timestamp + "." + rawBody`. The backend
rejects missing or invalid signatures, unknown organizations, and timestamps
outside the allowed clock skew. Duplicate event IDs are accepted idempotently
without scheduling duplicate work.

## Consequences

- Each OpenMRS organization has its own webhook secret.
- The OpenMRS OMOD persists failed deliveries in its outbox for retry.
- TLS remains required at the deployment boundary.
