# 18. Multi-OpenMRS hospital configuration

Date: 2026-05-30
Status: Accepted

## Context

The backend must support multiple OpenMRS O3 hospital deployments. Each hospital can have different OpenMRS credentials, webhook secret, timezone, poller behavior, default provider, provider credentials, and retry policy.

The old single-organization environment variables are still useful for local development, but they are not enough for a hospital-hosted backend that serves multiple OpenMRS instances.

## Decision

Use organization-scoped configuration:

- Preferred input: `HospitalConfiguration:Organizations` JSON.
- Startup seeding: upsert organization rows and provider rows into PostgreSQL.
- Runtime lookup: resolve enabled organization by `organization_id`.
- Secrets: store OpenMRS credentials, webhook secrets, and provider credential JSON encrypted.

Legacy `OpenMrs:*`, `Webhooks:OpenMrs:*`, and `Messaging:*` configuration can seed one default organization only when no JSON organizations are provided.

## Consequences

- One backend can serve multiple OpenMRS O3 deployments.
- Webhook authorization is organization-specific.
- Provider selection is organization-specific.
- Configuration changes become auditable database state after seeding.
- Operators must keep organization ids stable; changing an id creates a new tenant boundary.
