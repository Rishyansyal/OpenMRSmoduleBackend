# Architecture Report - OpenMRS Appointment Reminder Platform

**Status:** current platform architecture
**Scope:** `OpenMRSmoduleBackend`, the OpenMRS O3 distro in `../2.4-LU1-openMRS-Avans`, and the custom OpenMRS appointment webhook module
**Out of scope:** the removed custom Next.js frontend

## 1. Purpose

OpenMRS O3 is the clinical user interface and patient-record system. The communication backend adds reminder capabilities around it:

- receive signed OpenMRS appointment events;
- schedule 24-hour and 1-hour reminders;
- send messages through configured providers;
- keep durable delivery, retry, and audit state;
- support multiple OpenMRS hospital deployments through organization configuration;
- protect patient data with encryption and retention jobs.

## 2. System Context

See [C4 system context](c4/01-context.md).

OpenMRS O3 sends signed appointment events to the backend. The backend uses OpenMRS FHIR R4 to retrieve patient contact data when a reminder is due. Messaging providers deliver SMS/email to patients. Clinicians use OpenMRS O3, not a separate custom frontend.

## 3. Containers

See [C4 containers](c4/02-containers.md).

| Container | Responsibility |
|---|---|
| OpenMRS Gateway | Routes `/openmrs` browser and API traffic to the OpenMRS O3 frontend and backend. |
| OpenMRS O3 Frontend | Clinical EMR user interface used by clinicians. |
| OpenMRS Backend | OpenMRS REST/FHIR APIs plus the custom appointment webhook OMOD. |
| OpenMRS Database | MariaDB store for OpenMRS clinical and configuration data. |
| Communication Backend API and Workers | JWT auth, webhook validation, OpenMRS/FHIR calls, reminder workers, MassTransit consumers, provider adapters, retention, and metrics. |
| Communication Database | PostgreSQL store for identity, organization/provider config, appointment state, scheduled reminders, retry ledger fields, templates, message logs, and webhook logs. |
| Reminder Message Broker | RabbitMQ/MassTransit command transport for reminder delivery outside integration tests. |

Local development uses RabbitMQ as well. Only the `IntegrationTest` environment may use MassTransit in-memory transport.

## 4. Configuration And Multi-OpenMRS

See [multi-hospital system landscape](c4/07-multi-hospital-landscape.md).

The preferred configuration model is JSON under `HospitalConfiguration:Organizations`. Each organization has its own OpenMRS base URL, OpenMRS credentials, webhook secret, default provider, timezone, poller settings, retry policy, and provider credentials. The startup seeder writes encrypted organization/provider settings to PostgreSQL.

If the JSON organization list is empty, the seeder can build one legacy organization from `OpenMrs:*`, `Webhooks:OpenMrs:*`, and `Messaging:*` settings.

The organization id is part of runtime routing:

- webhooks use `X-OpenMRS-Organization-Id`;
- appointment, reminder, and webhook rows store `organization_id`;
- provider lookup is scoped to organization id and provider name.

## 5. Authentication

Public registration is not the default operating model. The backend bootstraps an admin user from `Admin:Email` and `Admin:Password`. `POST /auth/register` returns `403 PUBLIC_REGISTRATION_DISABLED` unless `Admin:AllowPublicRegistration` is explicitly enabled.

## 6. Webhook Security

See [signed appointment webhook dynamic view](c4/05-appointment-webhook-dynamic.md) and [webhook contract](webhook-openmrs-backend.md).

The backend validates required headers, organization status, timestamp skew, and HMAC before appointment processing. Duplicate event ids are accepted idempotently without duplicating appointment/reminder work.

## 7. Durable Delivery And Retry

See [due reminder delivery and retry dynamic view](c4/06-reminder-delivery-dynamic.md).

The webhook stores intent only. It does not call providers. The worker/consumer chain is:

1. `ReminderWorker` claims due reminders from PostgreSQL.
2. It publishes `SendReminderCommand`.
3. RabbitMQ transports commands outside integration tests.
4. `SendReminderConsumer` performs FHIR lookup and provider send.
5. PostgreSQL records queue message id, queued/consumed timestamps, `attempt_count`, `max_attempts`, retry delay settings, last/next attempt timestamps, provider message id, status, and audit logs.

Provider fallback is intentionally not automatic. A reminder uses the provider selected by the organization/default configuration. If that provider fails, the system retries the same provider, then records failure/dead-letter state.

## 8. Data Model

The data model is owned by EF Core in `Infrastructure/Persistence/ApplicationDbContext.cs`.
An ER diagram is not part of the C4 model.

Important tables:

- `organization_integration_configs`
- `organization_provider_configs`
- `appointment_notifications`
- `scheduled_reminders`
- `reminder_logs`
- `message_logs`
- `webhook_event_logs`
- Identity user tables

Sensitive OpenMRS and provider credentials are encrypted before storage. Appointment patient context is encrypted. Hash columns are used only where deterministic lookup/idempotency requires them.

## 9. Deployment

See [local deployment](c4/08-local-deployment.md) and [production/staging deployment](c4/09-production-deployment.md).

Production target:

- reverse proxy terminates TLS;
- API pods run controllers, workers, and consumers;
- RabbitMQ provides durable queueing;
- PostgreSQL stores configuration and ledger state;
- Prometheus scrapes `/metrics`;
- one or more OpenMRS O3 deployments integrate through signed webhooks and FHIR.

## 10. Decision Summary

Current ADRs to read first:

- [ADR-0008](adr/0008-webhook-for-openmrs-integration.md) - OpenMRS events enter through signed webhooks; poller is an explicitly configured fallback mode, not the default integration.
- [ADR-0009](adr/0009-masstransit-for-async-messaging.md) - MassTransit coordinates asynchronous reminder work.
- [ADR-0017](adr/0017-async-messaging-as-separate-component.md) - RabbitMQ/message bus is a separate scalability and failure domain.
- [ADR-0018](adr/0018-multi-openmrs-hospital-configuration.md) - organization-scoped OpenMRS and provider configuration.
- [ADR-0019](adr/0019-durable-rabbitmq-postgresql-retry-ledger.md) - RabbitMQ transport plus PostgreSQL retry ledger.
