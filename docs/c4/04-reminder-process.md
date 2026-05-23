# Procesdiagram — Afspraakherinnering Flow

Stap-voor-stap weergave van hoe een afspraakherinnering van OpenMRS naar de patiënt gaat.

## Automatische flow (elke 5 minuten)

```mermaid
sequenceDiagram
    autonumber
    participant W as ReminderWorker
    participant O as OpenMRS FHIR API
    participant DB as PostgreSQL
    participant B as MassTransit Bus
    participant C as SendReminderConsumer
    participant P as Messaging Provider
    participant Pat as Patiënt

    loop Elke 5 minuten
        W->>O: GET /Encounter?date=ge{23u}&date=le{25u} (24u-venster)
        O-->>W: Lijst van aankomende encounters
        W->>O: GET /Encounter?date=ge{55min}&date=le{65min} (1u-venster)
        O-->>W: Lijst van aankomende encounters

        loop Per encounter
            W->>DB: AlreadySentAsync(encounterId, window)?
            DB-->>W: false (nog niet verstuurd)
            W->>B: Publish SendReminderCommand
        end
    end

    B->>C: Deliver SendReminderCommand
    C->>DB: AlreadySentAsync(encounterId, window)? [idempotentie]
    DB-->>C: false

    C->>O: GET /Patient/{patientId}
    O-->>C: PatientContact (naam, telefoon, e-mail)

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
        C->>C: Log "overgeslagen" en stop
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
    W->>B: Publish SendReminderCommand (per encounter in venster)
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
        S->>DB: DELETE FROM reminder_logs WHERE encounter_start < now - 14 dagen
        DB-->>S: N records verwijderd
        S->>DB: DELETE FROM message_logs WHERE sent_at < now - 365 dagen
        DB-->>S: M records verwijderd
        S-->>W: DataRetentionResult(N, M)
        W->>W: LogInformation(N + M verwijderd)
    end
```
