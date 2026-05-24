# Security Review

| Risico | Status | Maatregel |
|---|---|---|
| Hardcoded providercredentials | Geen echte secrets gevonden in tracked config | `.env` is gitignored; `.env.example` bevat placeholders |
| Design-time DB password fallback | Opgelost | EF design-time factory vereist nu env var |
| Webhook spoofing | Opgelost | HMAC-SHA256 signature + timestamp-skew |
| Webhook replay | Opgelost | `event_id` unique + timestamp-skew |
| Patiëntdata in logs | Verbeterd | Reminder consumer logt encounter-id, geen naam/contact |
| Patiëntdata at rest | Verbeterd | Appointment patient/service/location/instructions encrypted met AES-256-GCM |
| RabbitMQ default credentials | Opgelost | Geen fallback meer; username/password zijn verplicht zodra `RabbitMq:Host` is gezet |
| OpenMRS demo credentials | Verbeterd | `.env.example` bevat placeholders; lokale `.env` moet eigen service-user credentials bevatten |
| OpenMRS debug port `5005` | Opgelost in default compose | Debug port wordt niet meer standaard gepubliceerd |
| JWT in localStorage | Geaccepteerd frontend ADR | Simpel dev-patroon; XSS-risico documenteren en CSP/sanitization toepassen |

GitHub Actions draait Gitleaks in backend, frontend en OpenMRS-project. Voorbeelden en placeholders blijven toegestaan; echte `.env` bestanden zijn genegeerd.
