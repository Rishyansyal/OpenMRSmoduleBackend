# 12. Signed OpenMRS webhook contract

Date: 2026-05-23

## Status

Accepted

## Context

De communicatiemodule moet gewijzigde en geannuleerde afspraken uit OpenMRS direct verwerken. Polling past niet bij ADR 0008 omdat updates dan vertraagd zijn en extra load veroorzaken.

## Decision

OpenMRS stuurt appointment/Encounter-events naar `POST /api/webhooks/openmrs/appointments`.

Iedere request bevat:

- `X-OpenMRS-Event-Id`
- `X-OpenMRS-Event-Type`
- `X-OpenMRS-Timestamp`
- `X-OpenMRS-Organization-Id`
- `X-OpenMRS-Signature: sha256=<hex>`

De signature is HMAC-SHA256 over `timestamp + "." + rawBody`. De backend verwerpt ontbrekende of ongeldige signatures, timestamps buiten de clock-skew en dubbele event-id's.

## Consequences

- OpenMRS en backend delen een webhook-secret per omgeving.
- Idempotency zit in de backend op `event_id`; dubbele events worden `200 OK` met `Duplicate = true`.
- OpenMRS houdt een outbox bij wanneer de backend tijdelijk niet bereikbaar is.
