# 6. Manage secrets outside tracked configuration

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

Database credentials, encryption keys, JWT keys, OpenMRS credentials, webhook
secrets, and provider credentials must not be committed to Git.

## Decision

- Keep local secrets in a gitignored `.env`.
- Keep placeholders in `.env.example`.
- Use deployment-platform secret storage for CI and production.
- Configure multiple hospitals with a gitignored JSON file based on
  `hospital-config.example.json`, mounted read-only into the API container.
- Encrypt OpenMRS and provider credentials before seeding them into PostgreSQL.

## Consequences

- Operators must create local secret files before startup.
- Secret rotation remains a deployment operation.
- Tracked examples document required keys without containing usable secrets.
