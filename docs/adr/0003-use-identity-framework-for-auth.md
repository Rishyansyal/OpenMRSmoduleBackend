# 3. Use ASP.NET Core Identity for authentication

Date: 2026-05-23

## Status

Accepted

## Context

We hebben authenticatie nodig (gebruikers, wachtwoorden, tokens). Eigen auth endpoints schrijven is een bekende foutbron: password hashing-fouten, timing-attacks, vergeten token-revocation, etc.

## Decision

We gebruiken **ASP.NET Core Identity** met `MapIdentityApi<IdentityUser>()`. Dat levert kant-en-klare endpoints voor register, login, refresh, confirm email, password reset, etc.

**Geen** handmatige `AuthController` of custom auth endpoints. Als we velden willen toevoegen aan de user, doen we dat via een class die overerft van `IdentityUser`.

Storage: EF Core met PostgreSQL provider (zie [ADR 0004](0004-use-postgresql.md)).

## Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **Zelf auth-endpoints schrijven (custom `AuthController` + JWT)** | Bekende foutbron: timing-attacks op login, password-hashing fouten, vergeten lockout, brute-force protection en token-revocation. Niet de wiel die we willen heruitvinden in een onderwijsproject. |
| **Duende IdentityServer / OpenIddict** | Volwaardige OIDC/OAuth2 server. Overkill voor één first-party client; Duende vereist commerciële licentie boven een omzetdrempel. |
| **Auth0 / Clerk / Supabase Auth** | Vendor-lock-in en kostenmodel ongewenst voor een Avans-project; data-residency lastig. |
| **JWT-only zonder users-tabel (stateless API-key per user)** | Geen account-management, geen lockout, geen password-reset; werkt niet voor dagelijks gebruik door zorgmedewerkers. |

## Consequences

- Snelle setup, security best practices (hashing, lockout, token expiry) door Microsoft onderhouden.
- Identity gebruikt EF Core; we hebben dus *zowel* EF Core (Identity) als Dapper (domain) in de stack. Acceptabele tradeoff (zie [ADR 0002](0002-use-dapper-as-orm.md)).
- Token format en endpoint shape worden bepaald door Microsoft, niet door ons. Voor externe consumenten betekent dit dat we de Identity API docs als referentie aanhouden, niet een eigen API spec.
- Migraties voor Identity tabellen lopen via EF Core migrations.
