# 6. Manage secrets via .env (gitignored)

Date: 2026-05-23

## Status

Accepted

## Context

Acceptatiecriterium: **geen hardcoded credentials**. We hebben minstens DB-credentials nodig en straks waarschijnlijk meer (externe API-keys, OpenMRS-koppelingen, etc.). Secrets mogen niet in git terechtkomen.

## Decision

- Echte secrets staan in een lokale **`.env`** in de project root. Dit bestand staat in `.gitignore` en wordt **nooit** gecommit.
- De canonieke kopie van `.env` leeft in de gedeelde **OneDrive**; teamleden halen 'm daar op.
- Het repo bevat een **[`.env.example`](../../.env.example)** met dezelfde keys maar placeholder-waarden, als documentatie.
- `docker-compose.yml` leest `.env` automatisch en injecteert de waarden als environment variables in de container.
- `appsettings.json` bevat geen credentials; `ConnectionStrings:DefaultConnection` is leeg en wordt overschreven door de env var `ConnectionStrings__DefaultConnection`.

## Consequences

- Geen credential leaks in git history.
- Onboarding-stap: nieuwe dev kopieert `.env` uit OneDrive. Minimaal extra werk.
- Voor productie/CI gebruiken we de geheime store van de target (Azure Key Vault, GitHub Actions secrets, etc.) — niet het `.env`-bestand. `.env` is alleen voor lokaal werk.
- Verifieer bij iedere PR (en in [security review](../../README.md)) dat `.env`, private keys of credentials niet per ongeluk gecommit zijn.
