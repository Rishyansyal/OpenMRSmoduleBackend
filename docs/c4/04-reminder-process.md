# Procesdiagram — Afspraakherinnering Flow

Stap-voor-stap weergave van hoe een afspraakherinnering van OpenMRS naar de patiënt gaat.

## Webhook en automatische flow

```mermaid
sequenceDiagram
    autonumber
    participant OM as OpenMRS webhookmodule
    participant API as OpenMrsWebhooksController
    participant WS as OpenMrsWebhookService
    participant W as ReminderWorker
    participant O as OpenMRS FHIR API
    participant DB as PostgreSQL
    participant B as MassTransit Bus
    participant C as SendReminderConsumer
    participant P as Messaging Provider
    participant Pat as Patiënt

    OM->>API: POST /api/webhooks/openmrs/appointments + HMAC headers
    API->>API: Validate HMAC, timestamp, required headers
    API->>WS: ProcessAppointmentAsync(event, payload)
    WS->>DB: Upsert appointment_notifications (encrypted fields)
    WS->>DB: Insert webhook_event_logs (payload hash)
    WS->>DB: Create/cancel scheduled_reminders (24h en 1h)

    loop Elke 5 minuten
        W->>DB: Claim due scheduled_reminders
        DB-->>W: Reminder dispatch records
        loop Per due reminder
            W->>B: Publish SendReminderCommand
        end
    end

    B->>C: Deliver SendReminderCommand
    C->>DB: AlreadySentAsync(encounterId, window)? [idempotentie]
    DB-->>C: false

    C->>O: GET /Patient/{patientId}
    O-->>C: PatientContact (telefoon, e-mail)

    alt Patiënt heeft contactgegevens
        C->>P: SendAsync(provider, recipient, content, type)
        P-->>C: SendMessageResult (success, messageId)
        C->>DB: LogAsync(ReminderLog)

        alt Versturen mislukt
            C-->>B: throw Exception
            B->>C: Retry (3x met backoff)
            Note over B,C: Na 3 pogingen → dead-letter queue
        else Versturen geslaagd
            C-->>Pat: SMS of e-mail ontvangen
        end
    else Geen contactgegevens
        C->>DB: Mark scheduled reminder failed (NO_CONTACT_DETAILS)
    end
```

## Handmatige trigger (voor testen)

```mermaid
sequenceDiagram
    autonumber
    actor Dev as Ontwikkelaar
    participant API as RemindersController
    participant W as ReminderWorker
    participant B as MassTransit Bus

    Dev->>API: POST /api/reminders/trigger (met JWT)
    API->>W: ProcessAsync()
    W->>B: Publish SendReminderCommand (per due scheduled reminder)
    B-->>W: Gepubliceerd
    W-->>API: Klaar
    API-->>Dev: 200 OK { "message": "Reminder-run voltooid." }
```

## Data-retentie flow (elke 24 uur)

```mermaid
sequenceDiagram
    autonumber
    participant W as DataRetentionWorker
    participant S as DataRetentionService
    participant DB as PostgreSQL

    loop Elke 24 uur
        W->>S: RunAsync()
        S->>DB: DELETE FROM appointment_notifications WHERE start_utc < now - 14 dagen
        DB-->>S: A records verwijderd
        S->>DB: DELETE FROM reminder_logs WHERE encounter_start < now - 14 dagen
        DB-->>S: N records verwijderd
        S->>DB: DELETE FROM message_logs WHERE sent_at < now - 365 dagen
        DB-->>S: M records verwijderd
        S->>DB: DELETE FROM webhook_event_logs WHERE received_at_utc < now - 365 dagen
        DB-->>S: W records verwijderd
        S-->>W: DataRetentionResult(N, M, A, W)
        W->>W: LogInformation(resultaat)
    end
```
