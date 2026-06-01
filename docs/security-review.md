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
| JWT opslag | Niet van toepassing — geen browser-frontend; tokens worden uitgewisseld via server-to-server of directe API-clients |
| Horizontale privilege-escalation op `/api/messages/history` | Opgelost | History gefilterd op `SentByUserId == currentUser`; bewezen door `AuthorizationTests.MessagesHistory_ReturnsOnlyCurrentUsersLogs`. Zie [auth-audit.md](auth-audit.md#f-1) |
| IDOR op `/api/messages/status/{trackingId}` | Opgelost | Ownership-check via `UserOwnsProviderMessageIdAsync` + 404 bij miss om bestaan niet te lekken; bewezen door `AuthorizationTests.MessagesStatus_ReturnsNotFound_ForOtherUsersTrackingId`. Zie [auth-audit.md](auth-audit.md#f-2) |
| OpenMRS proxy op verkeerde organisatie | Opgelost voor configuratiegrenzen | Proxy-calls vereisen een bekende, ingeschakelde `organizationId`; ontbrekende IDs gebruiken alleen de geconfigureerde default. Gebruikersrollen per ziekenhuis blijven een toekomstige uitbreiding. |
| Admin-acties zonder rol-onderscheid | Opgelost | Startup seedt de `Admin`-rol; reminderbeheer, handmatige retries, demo-webhooks en dataretentie vereisen het `AdminOnly`-beleid. |

GitHub Actions draait Gitleaks voor de backend en relevante OpenMRS-integratiecode. Voorbeelden en placeholders blijven toegestaan; echte `.env` bestanden zijn genegeerd. De eerdere custom frontend is geen onderdeel meer van deze architectuur.

Voor een endpoint-voor-endpoint authorization-audit, IDOR-checks, testdekking en de positieve bevindingen, zie [auth-audit.md](auth-audit.md).
