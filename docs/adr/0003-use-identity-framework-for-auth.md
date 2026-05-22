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

## Consequences

- Snelle setup, security best practices (hashing, lockout, token expiry) door Microsoft onderhouden.
- Identity gebruikt EF Core; we hebben dus *zowel* EF Core (Identity) als Dapper (domain) in de stack. Acceptabele tradeoff (zie [ADR 0002](0002-use-dapper-as-orm.md)).
- Token format en endpoint shape worden bepaald door Microsoft, niet door ons. Voor externe consumenten betekent dit dat we de Identity API docs als referentie aanhouden, niet een eigen API spec.
- Migraties voor Identity tabellen lopen via EF Core migrations.
