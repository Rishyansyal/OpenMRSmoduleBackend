# Requirements

## Functional Requirements

| ID | Requirement | Implementation |
|---|---|---|
| FE-1 | Patient receives appointment notifications 24h and 1h before the appointment | Signed OpenMRS event creates `scheduled_reminders`; `ReminderWorker` publishes due work; `SendReminderConsumer` sends through the configured provider. |
| FE-2 | Delivery information supports reporting and audit | `message_logs`, `reminder_logs`, `webhook_event_logs`, provider, status, timestamps, retry state, and provider message ids. |
| FE-3 | Messaging provider can differ per organization | `organization_integration_configs.default_provider` and `organization_provider_configs`. No automatic fallback to a different provider. |
| FE-4 | Multiple OpenMRS instances can connect to one backend | `X-OpenMRS-Organization-Id` selects organization-specific OpenMRS credentials, webhook secret, timezone, poller, provider, and retry settings. |
| FE-5 | Admin access exists without open public registration | Startup bootstraps an admin account; public registration is disabled unless configured. |

## Non-Functional Requirements

| ID | Requirement | Implementation / Status |
|---|---|---|
| NFE-1 | Standalone SaaS backend, multi-tenant | Backend is independently deployable; org id is carried in webhook headers and persisted on operational rows. |
| NFE-2 | Documented, secured OpenMRS O3 integration | Signed webhook contract and C4 docs describe OpenMRS O3 as the only UI-facing EMR dependency. |
| NFE-3 | Four providers | SwiftSend, SecurePost, LegacyLink, AsyncFlow. |
| NFE-4 | OpenMRS 2.7.x+ / O3-compatible deployment | Local target is OpenMRS O3 distro on port 3032. |
| NFE-5 | AES-256 and no secrets in code | Field encryption keys come from configuration; `.env` is gitignored; organization/provider secrets are encrypted before storage. |
| NFE-6 | HL7/FHIR | FHIR R4 client retrieves patient contact and encounter data from each configured OpenMRS instance. |
| NFE-7 | Retry and queueing | RabbitMQ + MassTransit retries in every non-test runtime; PostgreSQL scheduled reminder fields form the retry ledger. Provider fallback is intentionally not automatic. |
| NFE-8 | Character sets | JSON and XML provider calls use UTF-8. |
| NFE-9 | Observability | OpenTelemetry tracing and Prometheus `/metrics`. |
| NFE-10 | Patient data removed within 14 days | `DataRetentionWorker` deletes appointment/reminder patient data. |
| NFE-11 | Metadata retained for at most one year | Message and webhook metadata are retained for 365 days. |
| NFE-12 | Extensible to other OpenMRS modules/events | Webhook contract includes event/resource metadata and org id. |
| NFE-13 | Timezones | Operational timestamps are UTC; organization timezone is stored in config for display and scheduling context. |
