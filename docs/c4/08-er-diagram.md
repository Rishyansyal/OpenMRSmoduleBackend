# ER-diagram — Database schema

Het PostgreSQL-schema van de OpenMRS Communicatiemodule. Tabelnamen volgen `snake_case` conventie (geconfigureerd in [ApplicationDbContext](../../Infrastructure/Persistence/ApplicationDbContext.cs)).

## Overzicht

```mermaid
erDiagram
    USERS {
        uuid id PK
        text email "AES-256-GCM encrypted"
        text email_hash UK "HMAC-SHA256, deterministic lookup"
        text password_hash "BCrypt"
        timestamp created_at
    }

    APPOINTMENT_NOTIFICATIONS {
        uuid id PK
        text organization_id
        text encounter_id
        text status
        timestamp start_utc
        timestamp end_utc "nullable"
        boolean is_cancelled
        text patient_id_encrypted "AES-256-GCM"
        text patient_display_encrypted "AES-256-GCM, nullable"
        text service_type_encrypted "nullable"
        text location_encrypted "nullable"
        text instructions_encrypted "nullable"
        text last_event_id
        timestamp created_at_utc
        timestamp updated_at_utc
    }

    SCHEDULED_REMINDERS {
        uuid id PK
        uuid appointment_notification_id FK
        text organization_id
        text encounter_id
        text reminder_window "24h | 1h"
        timestamp scheduled_for_utc
        text provider
        text status "pending | queued | sent | cancelled | failed"
        text last_error_code "nullable"
        timestamp sent_at_utc "nullable"
        timestamp created_at_utc
        timestamp updated_at_utc
    }

    REMINDER_LOGS {
        uuid id PK
        text encounter_id "AES-256-GCM encrypted"
        text encounter_id_hash "HMAC-SHA256"
        text reminder_window "24h | 1h"
        text provider
        boolean success
        text error_code "nullable"
        timestamp encounter_start
        timestamp sent_at
    }

    MESSAGE_LOGS {
        uuid id PK
        text provider
        text message_type "sms | email | push"
        int recipient_count
        int failed_count
        text provider_message_id "nullable"
        boolean success
        text error_code "nullable"
        timestamp sent_at
        text sent_by_user_id "FK naar AspNetUsers.Id"
    }

    WEBHOOK_EVENT_LOGS {
        uuid id PK
        text event_id UK
        text event_type
        text organization_id
        text resource_type "default: Encounter"
        text resource_id
        text payload_sha256
        boolean duplicate
        boolean processed
        text error_code "nullable"
        timestamptz event_timestamp
        timestamp received_at_utc
    }

    ORGANIZATION_INTEGRATION_CONFIGS {
        uuid id PK
        text organization_id UK
        text default_provider "default: swiftsend"
        text time_zone_id "default: UTC"
        timestamp created_at_utc
        timestamp updated_at_utc
    }

    ASPNETUSERS {
        text Id PK
        text UserName
        text PasswordHash
        int AccessFailedCount
        boolean LockoutEnabled
        timestamp LockoutEnd
    }

    APPOINTMENT_NOTIFICATIONS ||--o{ SCHEDULED_REMINDERS : "heeft 0..2"
    ASPNETUSERS ||--o{ MESSAGE_LOGS : "verstuurt"
```

## Toelichting

### Encryptie (AES-256-GCM)
Velden met PII worden bij opslag versleuteld via `IEncryptionService` (ValueConverter in `OnModelCreating`). Voor zoeken op email/encounter is een aparte HMAC-SHA256-hash kolom — deterministisch en daardoor indexeerbaar.

| Tabel | Encrypted velden | Hash-kolom |
|---|---|---|
| `users` | `email` | `email_hash` (unique) |
| `reminder_logs` | `encounter_id` | `encounter_id_hash` (compound index met `reminder_window`) |
| `appointment_notifications` | `patient_id`, `patient_display`, `service_type`, `location`, `instructions` | — |

### Relaties
- **`scheduled_reminders` → `appointment_notifications`** (FK, ON DELETE CASCADE). Per afspraak max 2 reminders: één 24h vooraf, één 1h vooraf.
- **`message_logs.sent_by_user_id` → `AspNetUsers.Id`** (geen FK constraint — losse koppeling, want users kunnen worden verwijderd terwijl logs bewaard blijven).

### Indices
| Tabel | Index | Doel |
|---|---|---|
| `users` | `email_hash` UNIQUE | Login + uniciteitscheck |
| `reminder_logs` | `(encounter_id_hash, reminder_window)` | Idempotentie-check in `SendReminderConsumer` |
| `appointment_notifications` | `(organization_id, encounter_id)` UNIQUE | Upsert vanuit webhook |
| `scheduled_reminders` | `(status, scheduled_for_utc)` | `ReminderWorker` claim-query |
| `scheduled_reminders` | `(appointment_notification_id, reminder_window)` | Cancel/upsert reminders |
| `webhook_event_logs` | `event_id` UNIQUE | Idempotentie webhook |
| `webhook_event_logs` | `received_at_utc` | Data-retentie query |
| `organization_integration_configs` | `organization_id` UNIQUE | Per-org config lookup |
| `message_logs` | `sent_at` | History-paginering |

### Data-retentie ([ADR-0010](../adr/0010-data-retention-and-encryption-policy.md))
- **14 dagen** — `appointment_notifications`, `reminder_logs` (bevatten patiëntdata)
- **365 dagen** — `message_logs`, `webhook_event_logs` (alleen meta-informatie)

### Identity tabellen
Daarnaast bestaan de standaard ASP.NET Core Identity-tabellen (`AspNetUsers`, `AspNetRoles`, `AspNetUserClaims`, etc.) voor authenticatie. Deze worden beheerd door `IdentityDbContext<IdentityUser>` en zijn niet als domeinentiteit opgenomen.
