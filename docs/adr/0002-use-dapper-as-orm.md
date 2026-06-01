# 2. Use Dapper as ORM

Date: 2026-05-23

## Status

Superseded by [ADR 0020](0020-use-ef-core-for-persistence.md).

## Historical Decision

The first backend version selected Dapper for domain queries and EF Core only
for ASP.NET Core Identity.

## Reason For Superseding

The implemented backend now uses `ApplicationDbContext` for Identity and domain
persistence, including organization configuration, webhook events, appointment
notifications, scheduled reminders, retry ledger state, templates, and audit
logs. Keeping a separate persistence pattern for these related transactional
writes would add complexity without a practical benefit.
