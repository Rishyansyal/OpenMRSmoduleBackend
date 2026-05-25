# 10. Data retention and encryption policy for sensitive appointment data

Date: 2026-05-23

## Status

Accepted

## Context

Our system handles sensitive patient appointment data. Regulations (GDPR, HIPAA, local healthcare laws) and security best practices require:

1. Encryption at rest for all sensitive data.
2. Data minimization: retain only what is needed, for as long as it is needed.
3. Audit trail: know what was stored, when, and why.

## Decision

We implement a three-tier data retention and encryption policy:

**Tier 1: Active Appointments (0–24 hours before scheduled time)**
- Encrypted in database using application-level encryption (encrypted column values).
- Full sensitive details retained: patient name, contact, appointment notes.
- Purpose: send reminders, allow rescheduling.
- Stored in: PostgreSQL with `Npgsql` and application-level encryption (symmetric key, stored in secrets vault).

**Tier 2: Completed Appointments (24 hours after scheduled time — 14 days)**
- Retain for audit, analytics, and dispute resolution.
- After 24 hours: **de-identification**: remove sensitive fields (names, phone, email); keep only metadata (appointment ID, date, duration, provider ID, outcome).
- Encrypted at rest via PostgreSQL's encryption at rest (or application-level if required).

**Tier 3: Historical Data (14+ days)**
- Delete encrypted sensitive data entirely.
- Keep only de-identified metadata for long-term analytics and compliance.
- This is triggered by the daily cleanup service (see [ADR 0009](0009-masstransit-for-async-messaging.md)).

**Logging:**
- All logs are retained for 12 months.
- Logs containing sensitive data are themselves encrypted or redacted.
- After 12 months: delete logs via annual cleanup service.

## Considered alternatives

| Alternative | Why rejected |
|---|---|
| **Retain everything forever** | Violates GDPR Article 5 (data minimization); increases breach blast radius; legal liability grows unboundedly. |
| **Soft delete only (`deleted_at` flag)** | PII remains in the database, just hidden — still recoverable by anyone with DB access. Does not actually reduce risk. |
| **Archive to cold storage (S3 Glacier / tape) after 14 days** | Adds operational complexity (extra storage tier, restore procedure, encryption-key management twice); not required for our use case where post-14-day patient data has no operational value. |
| **Single retention period for everything (e.g., 30 days)** | Either deletes audit logs too soon (compliance gap for invoice/dispute resolution, 365 days needed) or keeps patient data too long. Three tiers match the actual purposes. |
| **No application-level encryption, rely on PostgreSQL TDE only** | Protects against stolen disks but not against an attacker with SQL access (insider, leaked credentials). App-level AES-GCM defends against both ([ADR-0015](0015-encryption-and-webhook-security-posture.md)). |
| **Full pseudonymization instead of deletion at 14 days** | Re-identification risk via metadata (date + provider + duration narrows down to a few patients). Outright deletion is safer and simpler. |

## Consequences

**Advantages:**
- Complies with data minimization principles (GDPR Article 5).
- Reduces liability: less sensitive data stored = lower breach impact.
- Audit trail: metadata is preserved indefinitely for compliance.
- Encryption at rest is transparent to application code (database handles it) if using PostgreSQL native encryption.

**Disadvantages:**
- Application must track appointment lifecycle (scheduled → completed → 14-day mark → deletion).
- Encryption keys must be managed securely; rotation is non-trivial.
- De-identification logic must be carefully designed to prevent re-identification.
- Testing data retention policies requires seeding old data and running cleanup jobs; adds test complexity.

**Implementation notes:**
- Use symmetric encryption (AES-256) with a key stored in a secrets vault (e.g., environment variables, Azure Key Vault).
- Implement the de-identification and cleanup jobs as MassTransit consumers.
- Log all deletions for compliance audits.
- Run migrations to backfill existing data with de-identification where applicable.
