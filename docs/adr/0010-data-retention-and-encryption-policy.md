# 10. Data retention and encryption policy

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

The backend stores patient-related appointment context and operational audit
records. It must minimize retained patient data while keeping enough metadata to
operate and investigate delivery failures.

## Decision

- Encrypt sensitive appointment context with application-level AES-256-GCM.
- Run `DataRetentionWorker` once every 24 hours.
- Delete `appointment_notifications` and `reminder_logs` older than
  `DataRetention:PatientDataRetentionDays` (default: 14).
- Delete `message_logs` older than `DataRetention:MessageLogRetentionDays`
  (default: 365).
- Delete `webhook_event_logs` older than
  `DataRetention:WebhookEventLogRetentionDays` (default: 365).
- Keep logs free of patient names, contact details, and message content.

The current implementation deletes expired records directly. It does not claim
to provide an intermediate de-identification archive.

## Consequences

- PostgreSQL does not retain patient context indefinitely.
- Operators can shorten retention periods through configuration.
- Cleanup is implemented by a hosted worker and can be triggered manually by an
  authorized administrator.
- Encryption-key rotation remains an operational procedure that requires a
  separate future decision.
