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

## Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **Credentials in `appsettings.json`** | Belandt in git history; precies wat het acceptatiecriterium "geen hardcoded credentials" verbiedt. |
| **`dotnet user-secrets`** | Werkt alleen tijdens `dotnet run` op de host; geen integratie met Docker Compose dat de container start. Voor onze Docker-baseline ([ADR-0005](0005-use-docker-for-deployment.md)) niet werkbaar. |
| **Encrypted secrets in repo (git-crypt / SOPS / age)** | Geeft committen aan git mogelijk, maar voegt key-management toe (wie heeft de decryption key?) — voor dev-credentials niet de moeite waard. |
| **Azure Key Vault / AWS Secrets Manager voor lokale dev** | Cloud-afhankelijkheid voor lokaal werk; vereist accounts en kostenmodel. Geadviseerd voor productie (zie consequences) maar niet voor dev. |
| **HashiCorp Vault zelf hosten** | Operationeel zwaar; overkill voor projectschaal. |

## Consequences

- Geen credential leaks in git history.
- Onboarding-stap: nieuwe dev kopieert `.env` uit OneDrive. Minimaal extra werk.
- Voor productie/CI gebruiken we de geheime store van de target (Azure Key Vault, GitHub Actions secrets, etc.) — niet het `.env`-bestand. `.env` is alleen voor lokaal werk.
- Verifieer bij iedere PR (en in [security review](../../README.md)) dat `.env`, private keys of credentials niet per ongeluk gecommit zijn.
