# Auth & Authorization Audit

**Datum:** 2026-05-25
**Branch:** `security-auth-audit`
**Reviewer:** projectteam
**Scope:** alle HTTP-endpoints in `OpenMRSmoduleBackend/Api/Controllers/` + globale autorisatie-policy in `Program.cs`.

Volledige uitwerking bij [security-review.md](security-review.md). Dit document is de detailaudit per endpoint.

---

## 1. Endpoint-inventarisatie

Alle endpoints, hun publieke status en welke check ze beschermt:

| Methode | Path | Auth | Bescherming | Bron |
|---|---|---|---|---|
| POST | `/auth/register` | Anonymous | Rate-limit 5/min | [AuthController.cs:13-31](../Api/Controllers/AuthController.cs#L13-L31) |
| POST | `/auth/login` | Anonymous | Rate-limit 5/min | [AuthController.cs:33-49](../Api/Controllers/AuthController.cs#L33-L49) |
| GET  | `/auth/me` | JWT | `[Authorize]` | [AuthController.cs:51-58](../Api/Controllers/AuthController.cs#L51-L58) |
| GET  | `/health` | Anonymous | — (liveness) | [HealthController.cs:13-15](../Api/Controllers/HealthController.cs#L13-L15) |
| GET  | `/health/db` | JWT | `[Authorize]` | [HealthController.cs:20-22](../Api/Controllers/HealthController.cs#L20-L22) |
| POST | `/api/webhooks/openmrs/appointments` | HMAC-SHA256 | Signed payload, timestamp-skew, replay-protection | [OpenMrsWebhooksController.cs](../Api/Controllers/OpenMrsWebhooksController.cs) |
| GET  | `/api/messages/providers` | JWT | `[Authorize]` op controller | [MessagesController.cs:21-23](../Api/Controllers/MessagesController.cs#L21-L23) |
| POST | `/api/messages` | JWT + rate-limit | `[EnableRateLimiting("MessagePolicy")]` 10/min | [MessagesController.cs:25-57](../Api/Controllers/MessagesController.cs#L25-L57) |
| GET  | `/api/messages/status/{trackingId}` | JWT + **ownership** | Eigenaarschap-check via `UserOwnsProviderMessageIdAsync` | [MessagesController.cs:59-74](../Api/Controllers/MessagesController.cs#L59-L74) |
| GET  | `/api/messages/history` | JWT + **per-user filter** | Filter op `SentByUserId == currentUser` | [MessagesController.cs:76-89](../Api/Controllers/MessagesController.cs#L76-L89) |
| GET  | `/api/openmrs/patients?q=` | JWT | `[Authorize]` | [OpenMrsController.cs:12-20](../Api/Controllers/OpenMrsController.cs#L12-L20) |
| GET  | `/api/openmrs/patients/{id}` | JWT | `[Authorize]` (zie F-3) | [OpenMrsController.cs:22-27](../Api/Controllers/OpenMrsController.cs#L22-L27) |
| GET  | `/api/openmrs/appointments` | JWT | `[Authorize]` | [OpenMrsController.cs:29-34](../Api/Controllers/OpenMrsController.cs#L29-L34) |
| POST | `/api/reminders/trigger` | JWT | `[Authorize]` (zie F-4) | [RemindersController.cs:20-26](../Api/Controllers/RemindersController.cs#L20-L26) |
| GET  | `/api/reminders/history` | JWT | `[Authorize]` (zie F-5) | [RemindersController.cs:28-34](../Api/Controllers/RemindersController.cs#L28-L34) |
| GET  | `/api/reminders/scheduled` | JWT | `[Authorize]` (zie F-5) | [RemindersController.cs:36-41](../Api/Controllers/RemindersController.cs#L36-L41) |
| GET  | `/api/reminders/templates` | JWT | `[Authorize]` | [RemindersController.cs:43-48](../Api/Controllers/RemindersController.cs#L43-L48) |
| PUT  | `/api/reminders/templates/{window}` | JWT | `[Authorize]` (zie F-4) | [RemindersController.cs:50-70](../Api/Controllers/RemindersController.cs#L50-L70) |
| POST | `/api/data-retention/trigger` | JWT | `[Authorize]` (zie F-4) | [DataRetentionController.cs:13-18](../Api/Controllers/DataRetentionController.cs#L13-L18) |

**Globale waarborg:** `Program.cs:307–316` zet zowel `DefaultPolicy` als `FallbackPolicy` op `RequireAuthenticatedUser()`. Een nieuwe controller die *geen* `[AllowAnonymous]` en *geen* `[Authorize]` heeft, is automatisch beschermd. Vergeten `[Authorize]` is daardoor geen single point of failure.

---

## 2. Bevindingen

### F-1 · **HIGH** — Cross-user lekkage in `/api/messages/history` (opgelost)

**Symptoom (voor fix):** `GET /api/messages/history` riep `messageLogRepository.GetRecentAsync(count)` aan zonder user-filter. Iedere ingelogde zorgmedewerker zag de verzendactiviteit van álle collega's, inclusief provider, type, aantallen ontvangers en foutcodes.

**Impact:** Horizontal privilege escalation. Hoewel `MessageLog` geen PII bevat ([Domain/MessageLog.cs](../Domain/MessageLog.cs)), is wie-wat-stuurt gevoelige bedrijfsinformatie en wettelijk privacygevoelig voor de zorgmedewerker.

**Fix:** Nieuwe interface-methode `GetRecentByUserAsync(string userId, int count, ct)` filtert op `SentByUserId == currentUser`. De controller haalt `userId` uit de `NameIdentifier`-claim van de JWT.

- [Application/Messaging/IMessageLogRepository.cs](../Application/Messaging/IMessageLogRepository.cs#L8-L13)
- [Infrastructure/Messaging/MessageLogRepository.cs](../Infrastructure/Messaging/MessageLogRepository.cs#L16-L21)
- [Api/Controllers/MessagesController.cs:76-89](../Api/Controllers/MessagesController.cs#L76-L89)

**Bewijs:** `AuthorizationTests.MessagesHistory_ReturnsOnlyCurrentUsersLogs` seedt 2 logs voor user A en 1 voor user B; verifieert dat ieder alleen zijn eigen ziet.

### F-2 · **MEDIUM** — IDOR via tracking-id in `/api/messages/status/{trackingId}` (opgelost)

**Symptoom (voor fix):** Iedere ingelogde gebruiker met een geldig of geraden tracking-id (AsyncFlow provider-message-id, opaque string) kon de afleverstatus van het bijbehorende bericht opvragen. Geen ownership-controle.

**Impact:** IDOR. Een aanvaller die tracking-ids kan enumereren (bv. via geleakte URL's, log-files, of brute-force op opaque strings) ziet afleverstatussen van vreemde berichten.

**Fix:** `UserOwnsProviderMessageIdAsync(userId, providerMessageId)` checkt of het log-record voor deze user bestaat. Bij miss: `404 Not Found` (bewust geen `403` om niet te lekken of het tracking-id überhaupt bestaat).

- [Api/Controllers/MessagesController.cs:59-74](../Api/Controllers/MessagesController.cs#L59-L74)
- [Infrastructure/Messaging/MessageLogRepository.cs:23-26](../Infrastructure/Messaging/MessageLogRepository.cs#L23-L26)

**Bewijs:** `AuthorizationTests.MessagesStatus_ReturnsNotFound_ForOtherUsersTrackingId` verifieert dat user B een tracking-id van user A niet kan bevragen.

### F-3 · **MEDIUM (geaccepteerd)** — IDOR op `/api/openmrs/patients/{id}`

**Symptoom:** De endpoint is een proxy naar OpenMRS FHIR R4. Iedere ingelogde zorgmedewerker kan elke patiënt-ID opvragen die in de OpenMRS-instance staat — er is geen scope-check op organisatie of behandelrelatie.

**Impact:** Bij multi-tenant deployment kan een zorgmedewerker van organisatie A patiëntdata van organisatie B inzien. In de huidige single-tenant inrichting is impact beperkt tot "alle zorgmedewerkers binnen één klinieksysteem zien alle patiënten" — wat overeenkomt met OpenMRS' eigen autorisatiemodel.

**Status:** Geaccepteerd risico voor MVP. NFE-1 noemt multi-tenancy, maar er is nog geen user↔organisatie-koppeling. Mitigatie:
- Korte termijn: documenteren als bekende beperking in [requirements.md](requirements.md) NFE-1.
- Lange termijn: voeg `OrganizationId` toe aan `User`-tabel, scope OpenMRS-queries op org, of geef elke org een eigen OpenMRS service-account.

**Bewijs:** `AuthorizationTests.ProtectedEndpoints_RequireAuthentication` verifieert wel dat het endpoint zonder JWT 401 geeft.

### F-4 · **LOW (geaccepteerd)** — Geen rol-onderscheid voor admin-acties

**Symptoom:** Drie endpoints zijn semantisch admin-only maar staan open voor iedere ingelogde user:
- `POST /api/data-retention/trigger` — triggert bulk-deletes (alleen verlopen data, beperkte impact).
- `POST /api/reminders/trigger` — start een reminder-run (kan dubbele berichten veroorzaken als er een race is met de scheduler, maar idempotency-check vangt dit op).
- `PUT /api/reminders/templates/{window}` — wijzigt de tekst van álle 24h/1h reminder-berichten voor alle gebruikers.

**Impact:** Een misleide of kwaadwillende user kan de retentie vervroegen, reminders triggeren, of templates wijzigen. Geen data-leak; wél integriteits-/availability-risico voor templates.

**Status:** Geaccepteerd risico voor MVP — er is nog geen rollen-systeem (`IdentityRole` wordt niet gebruikt; JWT claims bevatten alleen `NameIdentifier`, `Email`, `Jti`). Mitigatie:
- Korte termijn: documenteren in deze audit.
- Lange termijn: voeg `Admin` rol toe via Identity-rollen, decoreer endpoints met `[Authorize(Roles = "Admin")]`, registreer eerste-user-als-admin in `AuthService.RegisterAsync`.

### F-5 · **LOW (geaccepteerd)** — `/api/reminders/history` en `/scheduled` tonen organisatie-brede data

**Symptoom:** Beide endpoints tonen alle reminder-records ongeacht eigenaar. Geen `OrganizationId` op `ReminderLog`/`ScheduledReminder` → filteren is zonder schema-wijziging onmogelijk.

**Impact:** In multi-tenant deployment lekken reminders tussen organisaties. In single-tenant: alle medewerkers zien álle reminders, wat overeenkomt met UC3 ("audit en factuurcontrole" — collectief inzicht is gewenst).

**Status:** Geaccepteerd. Zie F-3 voor de multi-tenancy-uitbreiding die dit ook oplost.

---

## 3. Verifieerde positieve bevindingen

| Onderwerp | Verificatie |
|---|---|
| **Authenticatie globaal afgedwongen** | `FallbackPolicy = RequireAuthenticatedUser()` ([Program.cs:313](../Program.cs#L313)). Bewezen door `AuthorizationTests.ProtectedEndpoints_RequireAuthentication` over 13 endpoints. |
| **Publieke endpoints expliciet gemarkeerd** | Alleen `[AllowAnonymous]` op `/auth/register`, `/auth/login`, `/health`, `/api/webhooks/openmrs/appointments`. |
| **Webhook authenticeert via HMAC** | `OpenMrsWebhookSignatureValidator` voor controller-logic; ongeldige signature → 401. Bewezen door `BackendIntegrationTests.AppointmentWebhook_RejectsInvalidSignature`. |
| **Email-enumeration tegengegaan** | `Register` geeft generieke `Conflict("A user with the provided details already exists.")` zonder verschil tussen "bestaat" en "wachtwoord ongeldig" — geen username-enumeration via 200/409. |
| **Account lockout** | Toegepast bij login via ASP.NET Core Identity defaults; rate-limit 5/min/IP op `/auth/login`. |
| **JWT-validatie strikt** | `ValidateIssuer`, `ValidateAudience`, `ValidateLifetime`, `ValidateIssuerSigningKey` allemaal `true`. `ClockSkew = TimeSpan.Zero` ([Program.cs:289-300](../Program.cs#L289-L300)). |
| **JWT-secret minimaal 32 bytes** | Gevalideerd in `AuthService.GenerateToken`. |
| **Wachtwoorden gehashed met BCrypt work factor 12** | [AuthService.cs:14](../Infrastructure/Auth/AuthService.cs#L14). |
| **CSRF-bescherming** | SameSite=Strict cookies + JWT in header (geen cookie-auth) maakt CSRF lastig. |

---

## 4. Testdekking

Nieuwe testbestand: [OpenMRSmoduleBackend.Tests/Integration/AuthorizationTests.cs](../OpenMRSmoduleBackend.Tests/Integration/AuthorizationTests.cs).

| Test | Wat het bewijst |
|---|---|
| `ProtectedEndpoints_RequireAuthentication` (Theory, 13 paden) | Elke beveiligde endpoint geeft 401 zonder JWT |
| `Health_IsAnonymouslyAccessible` | Liveness-probe werkt zonder auth |
| `Register_IsAnonymouslyAccessible` | Registratie-endpoint open |
| `Login_DoesNotReturn401_WithoutPriorAuth` | Login endpoint zelf bereikbaar |
| `MessagesHistory_ReturnsOnlyCurrentUsersLogs` | F-1 fix bewezen — cross-user history-isolatie |
| `MessagesStatus_ReturnsNotFound_ForOtherUsersTrackingId` | F-2 fix bewezen — IDOR-bescherming op tracking-id |

Resultaat: **30/30 tests groen** (`dotnet test`).

---

## 5. Acceptatiecriteria — checklist

| Criterium | Bewijs |
|---|---|
| Beveiligde endpoints vereisen authenticatie | `FallbackPolicy` + 13-pad theorie-test |
| Gebruikers kunnen geen data van andere gebruikers benaderen | F-1 + F-2 opgelost en getest; F-3/F-5 gedocumenteerd geaccepteerd risico |
| Admin functionaliteit is niet toegankelijk voor normale gebruikers | F-4 gedocumenteerd geaccepteerd risico; geen rollen-systeem in scope MVP |
| Authorization checks zijn getest | Zie §4 |
| Bevindingen zijn vastgelegd | Dit document + [security-review.md](security-review.md) |
