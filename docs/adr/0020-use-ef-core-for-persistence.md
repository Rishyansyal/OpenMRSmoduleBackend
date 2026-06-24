# 20. Use EF Core for application persistence

Date: 2026-05-30

## Status

Accepted. Supersedes [ADR 0002](0002-use-dapper-as-orm.md).

## Context

Reliable webhook processing and reminder delivery require transactional updates
across appointments, scheduled reminders, retry state, audit logs, and
organization configuration. ASP.NET Core Identity already uses EF Core.

## Decision

Use EF Core `ApplicationDbContext` and EF migrations for Identity and domain
persistence. Keep SQL explicit where concurrency behavior matters, and use
database transactions for reminder claiming.

## Consequences

- One migration path owns the PostgreSQL schema.
- Domain writes can participate in consistent transactions.
- Repository queries remain reviewable and testable with SQLite integration
  databases.
- Dapper is not the domain persistence strategy.
