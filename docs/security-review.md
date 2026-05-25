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
| Horizontale privilege-escalation op `/api/messages/history` | Opgelost | History gefilterd op `SentByUserId == currentUser`; bewezen door `AuthorizationTests.MessagesHistory_ReturnsOnlyCurrentUsersLogs`. Zie [auth-audit.md](auth-audit.md#f-1) |
| IDOR op `/api/messages/status/{trackingId}` | Opgelost | Ownership-check via `UserOwnsProviderMessageIdAsync` + 404 bij miss om bestaan niet te lekken; bewezen door `AuthorizationTests.MessagesStatus_ReturnsNotFound_ForOtherUsersTrackingId`. Zie [auth-audit.md](auth-audit.md#f-2) |
| IDOR op `/api/openmrs/patients/{id}` | Geaccepteerd (MVP) | Multi-tenancy uit scope; in single-tenant identiek aan OpenMRS' eigen autorisatie. Zie [auth-audit.md](auth-audit.md#f-3) |
| Admin-acties zonder rol-onderscheid (`/api/data-retention/trigger`, `/api/reminders/trigger`, `/api/reminders/templates/{window}`) | Geaccepteerd (MVP) | Geen rollen-systeem; lange termijn `[Authorize(Roles="Admin")]`. Zie [auth-audit.md](auth-audit.md#f-4) |

GitHub Actions draait Gitleaks in backend, frontend en OpenMRS-project. Voorbeelden en placeholders blijven toegestaan; echte `.env` bestanden zijn genegeerd.

Voor een endpoint-voor-endpoint authorization-audit, IDOR-checks, testdekking en de positieve bevindingen, zie [auth-audit.md](auth-audit.md).
