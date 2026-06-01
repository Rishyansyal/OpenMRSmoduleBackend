# Reminder Process

## Webhook To Durable Reminder State

```mermaid
sequenceDiagram
    autonumber
    participant OM as OpenMRS O3
    participant API as Webhook Controller
    participant CFG as Org Config
    participant SVC as Webhook Service
    participant DB as PostgreSQL

    OM->>API: POST appointment event + org id + timestamp + HMAC
    API->>CFG: Resolve enabled organization
    API->>API: Validate timestamp and HMAC
    API->>SVC: Process appointment event
    SVC->>DB: Deduplicate event id
    SVC->>DB: Upsert encrypted appointment notification
    SVC->>DB: Create/cancel 24h and 1h scheduled reminders
    SVC->>DB: Store selected organization default provider
    API-->>OM: 200 OK accepted or duplicate
```

## Durable Delivery And Retry

```mermaid
sequenceDiagram
    autonumber
    participant W as ReminderWorker
    participant DB as PostgreSQL retry ledger
    participant MQ as RabbitMQ / MassTransit
    participant C as SendReminderConsumer
    participant OM as OpenMRS FHIR
    participant P as Configured Provider

    W->>DB: Claim due or retry-ready scheduled reminders
    DB-->>W: Dispatch records
    W->>MQ: Publish SendReminderCommand
    MQ->>C: Deliver command
    C->>DB: Check reminder idempotency
    C->>OM: Get patient contact
    C->>P: Send message through configured provider
    alt Success
        C->>DB: Mark sent, store provider message id, log success
    else Transient failure
        C->>DB: Increment attempt_count, set last_error_code and next_attempt_at_utc
        C-->>MQ: Throw for MassTransit retry/dead-letter behavior
    else Permanent failure or max attempts
        C->>DB: Mark failed/dead_lettered and log failure
    end
```

No automatic provider fallback occurs. Retries target the same provider selected by organization configuration unless an operator changes configuration and intentionally requeues work.

## Manual Trigger

```mermaid
sequenceDiagram
    autonumber
    actor Admin as Admin/API client
    participant API as RemindersController
    participant W as ReminderWorker
    participant MQ as RabbitMQ / MassTransit

    Admin->>API: POST /api/reminders/trigger with JWT
    API->>W: ProcessAsync()
    W->>MQ: Publish commands for due reminders
    API-->>Admin: 200 OK
```

## Retention

```mermaid
sequenceDiagram
    autonumber
    participant W as DataRetentionWorker
    participant S as DataRetentionService
    participant DB as PostgreSQL

    W->>S: RunAsync()
    S->>DB: Delete appointment/reminder patient data older than 14 days
    S->>DB: Delete message/webhook metadata older than 365 days
    S-->>W: DataRetentionResult
```
