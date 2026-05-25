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

## Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **Geen authenticatie, alleen TLS** | Iedereen die het endpoint kent kan webhooks injecteren; ongeacceptabel voor patiëntdata. |
| **Shared bearer token in header** | Vatbaar voor replay-attacks zodra het token gelogd of gelekt is; geen timestamp- of body-binding. HMAC + timestamp lost dit op. |
| **Mutual TLS (mTLS)** | Sterke authenticatie, maar vereist certificate-management aan OpenMRS-zijde dat de huidige OpenMRS-webhookmodule niet ondersteunt zonder fors uitbreiden. Pragmatisch nu te zwaar. |
| **OAuth2 client_credentials grant** | Zou werken, maar voegt een token-endpoint en token-cache toe tussen twee systemen die elkaar al direct kennen. Disproportioneel. |
| **IP-allowlist op de reverse proxy** | Brittle (NAT, dynamische IP's bij hosted OpenMRS, multi-tenant deployments); geen integriteitsgarantie op de body. Hooguit een **extra** verdediging bovenop HMAC, niet als enige. |
| **Asymmetrische signing (RSA / Ed25519)** | Voorkomt dat de backend de webhook ook zou kunnen *produceren*, maar voor een one-way integratie zonder verdere claims onnodige complexiteit. Symmetrische HMAC is industriestandaard hiervoor (zie GitHub, Stripe, Slack). |

## Consequences

- OpenMRS en backend delen een webhook-secret per omgeving.
- Idempotency zit in de backend op `event_id`; dubbele events worden `200 OK` met `Duplicate = true`.
- OpenMRS houdt een outbox bij wanneer de backend tijdelijk niet bereikbaar is.
