# Requirements

## Functionele requirements

| ID | Requirement | Implementatie |
|---|---|---|
| FE-1 | Patiënt ontvangt afspraaknotificaties 24u en 1u vooraf | Webhook maakt `scheduled_reminders`; `ReminderWorker` verstuurt via provider |
| FE-2 | Verzendinformatie is bruikbaar voor rapportage/factuurcontrole | `message_logs`, `reminder_logs`, provider, type, aantallen, status, timestamps |
| FE-3 | Messaging provider per organisatie | `organization_integration_configs.default_provider`, fallback `Reminders:DefaultProvider` |

## Niet-functionele requirements

| ID | Requirement | Implementatie / status |
|---|---|---|
| NFE-1 | Zelfstandig SaaS-proces, multi-tenant | Backend draait zelfstandig; webhook header bevat organization id |
| NFE-2 | Gedocumenteerde, beveiligde OpenMRS-integratie | Zie `docs/webhook-openmrs-backend.md` en ADR 0012 |
| NFE-3 | Vier providers | SwiftSend, SecurePost, LegacyLink, AsyncFlow |
| NFE-4 | OpenMRS 2.7.x+ | LU1 distro gebruikt OpenMRS 2.8.6 |
| NFE-5 | AES-256 en geen secrets in code/config | AES-256-GCM via env key; `.env` gitignored |
| NFE-6 | HL7/FHIR | FHIR R4 client blijft beschikbaar voor patiëntcontact en encounters |
| NFE-7 | Retry/fallback/queueing | MassTransit retry; OpenMRS outbox voor webhook failures |
| NFE-8 | Karaktersets | UTF-8 JSON/XML; provider adapters gebruiken UTF-8 |
| NFE-9 | Observability | OpenTelemetry + Prometheus `/metrics` |
| NFE-10 | Patiëntdata verwijderen binnen 14 dagen | `DataRetentionWorker` verwijdert appointment/reminder data |
| NFE-11 | Metadata max 1 jaar | Message/webhook logs retention op 365 dagen |
| NFE-12 | Uitbreidbaar naar andere modules | Webhookcontract kan resource types uitbreiden |
| NFE-13 | Tijdzones | Timestamps worden UTC opgeslagen; organisatie-timezone staat in config voor uitbreiding |
