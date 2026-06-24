# OpenMRS O3 Appointment Webhook

The backend accepts appointment events from OpenMRS O3 and turns them into scheduled 24-hour and 1-hour reminders. The webhook is backend-only; there is no custom Next.js frontend in the current architecture.

## Endpoint

```text
POST /api/webhooks/openmrs/appointments
```

| Header | Required | Meaning |
|---|---:|---|
| `X-OpenMRS-Event-Id` | yes | Unique idempotency key per OpenMRS event. |
| `X-OpenMRS-Event-Type` | yes | For example `CREATED`, `UPDATED`, `VOIDED`, `UNVOIDED`, or cancellation-specific event names. |
| `X-OpenMRS-Timestamp` | yes | UTC ISO-8601 timestamp. |
| `X-OpenMRS-Organization-Id` | yes | Organization/hospital id used to select config and secrets. |
| `X-OpenMRS-Signature` | yes | `sha256=<hex hmac>`. |

Signature:

```text
hex(hmac_sha256(organization_webhook_secret, timestamp + "." + raw_json_body))
```

The organization id is part of authorization. The backend must find an enabled organization config and validate the HMAC with that organization's secret. A missing or disabled organization is rejected; the backend does not silently fall back to another hospital config.

## Payload

```json
{
  "encounterId": "enc-123",
  "patientId": "patient-456",
  "start": "2026-05-25T10:00:00Z",
  "end": "2026-05-25T10:20:00Z",
  "status": "planned",
  "patientDisplay": "Do not log",
  "serviceType": "Controle",
  "location": "Polikliniek A",
  "instructions": "Medicijnen meenemen"
}
```

Sensitive fields are encrypted before storage. The webhook event log stores only event metadata, resource id, payload hash, duplicate flag, processed flag, and error code.

## Processing

1. Validate required headers.
2. Resolve the enabled organization by `X-OpenMRS-Organization-Id`.
3. Validate timestamp skew.
4. Validate HMAC over the exact raw body.
5. Deduplicate by `X-OpenMRS-Event-Id`.
6. Upsert `appointment_notifications` for `(organization_id, encounter_id)`.
7. Encrypt patient and appointment context fields.
8. Recompute 24h and 1h `scheduled_reminders`.
9. Use the organization's configured `default_provider`; do not auto-fallback to another provider.
10. Persist state in PostgreSQL and return success for accepted duplicate or processed events.

## Durable Delivery

The webhook only records appointment/reminder intent. It does not call providers directly.

Delivery is handled later:

1. `ReminderWorker` claims due reminders from PostgreSQL.
2. It publishes `SendReminderCommand` through MassTransit.
3. Outside `IntegrationTest`, MassTransit uses RabbitMQ.
4. `SendReminderConsumer` retrieves patient contact through OpenMRS FHIR and calls the selected provider.
5. Queue message id, queued timestamp, consumed timestamp, success, provider message id, error code, attempt count, and next attempt state are retained in PostgreSQL.
6. Repeated failures are retried according to the ledger and bus policy, then marked failed/dead-lettered.

## Example Request

```bash
BODY='{"encounterId":"enc-123","patientId":"patient-456","start":"2026-05-25T10:00:00Z","status":"planned"}'
TS="$(date -u +"%Y-%m-%dT%H:%M:%SZ")"
SIG="$(printf "%s.%s" "$TS" "$BODY" | openssl dgst -sha256 -hmac "$OPENMRS_WEBHOOK_SECRET" -hex | sed 's/^.* //')"

curl -X POST http://localhost:5111/api/webhooks/openmrs/appointments \
  -H "Content-Type: application/json" \
  -H "X-OpenMRS-Event-Id: evt-123" \
  -H "X-OpenMRS-Event-Type: CREATED" \
  -H "X-OpenMRS-Timestamp: $TS" \
  -H "X-OpenMRS-Organization-Id: openmrs-local" \
  -H "X-OpenMRS-Signature: sha256=$SIG" \
  -d "$BODY"
```
