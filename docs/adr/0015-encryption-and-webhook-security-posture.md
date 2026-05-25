# 15. Encryption and webhook security posture

Date: 2026-05-23

## Status

Accepted

## Context

Webhook-events bevatten patiënt- en afspraakdata. De opdracht vereist AES-256 voor opslag, TLS voor transport, geen hardcoded credentials en geen onbeveiligde gevoelige data in logs.

## Decision

De backend versleutelt gevoelige afspraakvelden met AES-256-GCM via `Security:EncryptionKey`. De key komt uit `SECURITY_ENCRYPTION_KEY` en wordt niet in tracked config opgeslagen.

Webhook-authenticatie gebruikt HMAC-SHA256 met replaybescherming. De backend bewaart geen raw payload en geen message body, alleen metadata en een SHA-256 payloadhash.

## Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **Geen versleuteling, alleen access control** | Voldoet niet aan opdrachteis AES-256; bij DB-leak of misbruikte query liggen patiëntgegevens direct open. |
| **PostgreSQL `pgcrypto`** | Werkt op SQL-niveau, maar de key komt dan vaak in de connection string of een DB-rol terecht; bovendien kunnen we niet meer met Dapper streamen zonder per query encryptie aan te roepen. App-level encryptie via `IFieldEncryptionService` (ValueConverter in EF Core) houdt encryptie transparant voor de domeincode. |
| **PostgreSQL Transparent Data Encryption (TDE)** | Beschermt alleen tegen *fysieke* diefstal van schijven; iemand met SQL-access ziet de cleartext alsnog. App-level AES-GCM beschermt tegen beide. |
| **AES-256-CBC i.p.v. GCM** | CBC vereist een aparte MAC voor integriteit (anders padding-oracle-risico); GCM levert authenticated encryption in één primitive. Standaardadvies (NIST SP 800-38D). |
| **RSA / asymmetrische versleuteling van velden** | Veel trager en groter ciphertext; geen gebruikssituatie waarin we het publieke-sleutel-voordeel nodig hebben. |
| **Hardware Security Module (HSM) voor key-opslag** | Sterk, maar kostbaar en operationeel zwaar; mogelijk in toekomstige iteratie. Voor nu volstaat een environment-variable + managed secrets in productie ([ADR-0006](0006-secrets-via-env-file.md)). |

Voor de webhook-authenticatie zijn alternatieven (mTLS, bearer token, OAuth2) afgewogen in [ADR-0012](0012-signed-openmrs-webhook-contract.md).

## Consequences

- Zonder encryptiesleutel start de API wel, maar webhookverwerking retourneert een configuratiefout.
- Key-rotatie is nog handmatig; een toekomstig ADR moet key-versioning beschrijven.
- Logs mogen geen patiëntnaam, telefoonnummer, e-mailadres of berichtinhoud bevatten.
