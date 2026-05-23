# 15. Encryption and webhook security posture

Date: 2026-05-23

## Status

Accepted

## Context

Webhook-events bevatten patiënt- en afspraakdata. De opdracht vereist AES-256 voor opslag, TLS voor transport, geen hardcoded credentials en geen onbeveiligde gevoelige data in logs.

## Decision

De backend versleutelt gevoelige afspraakvelden met AES-256-GCM via `Security:EncryptionKey`. De key komt uit `SECURITY_ENCRYPTION_KEY` en wordt niet in tracked config opgeslagen.

Webhook-authenticatie gebruikt HMAC-SHA256 met replaybescherming. De backend bewaart geen raw payload en geen message body, alleen metadata en een SHA-256 payloadhash.

## Consequences

- Zonder encryptiesleutel start de API wel, maar webhookverwerking retourneert een configuratiefout.
- Key-rotatie is nog handmatig; een toekomstig ADR moet key-versioning beschrijven.
- Logs mogen geen patiëntnaam, telefoonnummer, e-mailadres of berichtinhoud bevatten.
