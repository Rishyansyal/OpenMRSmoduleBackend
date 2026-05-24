# Architecture Decision Records

Korte log van significante architectuurkeuzes voor deze backend. Iedere ADR beschrijft *één* beslissing in het format: **Context → Decision → Consequences**.

Nieuwe ADR? Kopieer een bestaande, verhoog het nummer, en zet de status op `Proposed` tot er consensus is.

## Index

- [0001 — Record architecture decisions](0001-record-architecture-decisions.md)
- [0002 — Use Dapper as ORM](0002-use-dapper-as-orm.md)
- [0003 — Use ASP.NET Core Identity for authentication](0003-use-identity-framework-for-auth.md)
- [0004 — Use PostgreSQL as primary database](0004-use-postgresql.md)
- [0005 — Use Docker for local dev and deployment](0005-use-docker-for-deployment.md)
- [0006 — Manage secrets via .env (gitignored)](0006-secrets-via-env-file.md)
- [0007 — Use Next.js 16.2.4 for SaaS frontend](0007-nextjs-for-frontend.md)
- [0008 — Use webhooks for OpenMRS integration, not polling APIs](0008-webhook-for-openmrs-integration.md)
- [0009 — Use MassTransit for asynchronous messaging and background services](0009-masstransit-for-async-messaging.md)
- [0010 — Data retention and encryption policy for sensitive appointment data](0010-data-retention-and-encryption-policy.md)
- [0011 — Use a layered folder structure within a single project](0011-layered-folder-structure.md)
- [0012 — Signed OpenMRS webhook contract](0012-signed-openmrs-webhook-contract.md)
- [0013 — Pragmatic automated test strategy](0013-pragmatic-automated-test-strategy.md)
- [0014 — CI quality gates](0014-ci-quality-gates.md)
- [0015 — Encryption and webhook security posture](0015-encryption-and-webhook-security-posture.md)
