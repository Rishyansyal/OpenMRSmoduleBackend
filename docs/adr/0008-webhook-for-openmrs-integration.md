# 8. Use signed webhooks for OpenMRS integration

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

The backend must receive appointment changes from OpenMRS O3. Appointment reminders must be created quickly after OpenMRS changes, and the backend must support more than one OpenMRS deployment.

## Decision

OpenMRS appointment changes enter the backend through signed webhooks:

- `POST /api/webhooks/openmrs/appointments`
- `X-OpenMRS-Organization-Id` identifies the hospital/tenant.
- `X-OpenMRS-Signature` is an HMAC over `timestamp + "." + raw_body`.
- `X-OpenMRS-Event-Id` is the idempotency key.

The backend validates organization, timestamp, and signature before processing. OpenMRS FHIR R4 remains the read-side integration for patient contact and encounter details.

Polling is not the default integration. The poll worker is an explicitly configured compatibility mode for environments where the OpenMRS webhook module is unavailable.

## Considered Alternatives

| Alternative | Why rejected |
|---|---|
| Poll OpenMRS REST/FHIR as the primary integration | More latency, more load, and more bookkeeping than event delivery. |
| Direct OpenMRS database reads | Tight schema coupling and poor security boundary. |
| Shared broker between OpenMRS and the backend | Operationally heavy for OpenMRS deployments. HTTP webhooks are simpler. |
| Manual API calls initiating reminder flows | Easy to miss and dependent on user behavior; OpenMRS events are the source of truth. |

## Consequences

- Appointment changes arrive event-first and can be idempotently recorded.
- Each OpenMRS deployment gets its own webhook secret and organization id.
- The webhook must be reachable from OpenMRS through a secure network path.
- Duplicate and replayed events must be safe.
- The backend must never silently authorize a webhook using another organization's secret.
