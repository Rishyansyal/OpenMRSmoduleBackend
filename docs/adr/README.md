# Architecture Decision Records

Significant architecture choices for `OpenMRSmoduleBackend`.

## Active Decisions

- [0001 - Record architecture decisions](0001-record-architecture-decisions.md)
- [0003 - Use ASP.NET Core Identity for authentication](0003-use-identity-framework-for-auth.md)
- [0004 - Use PostgreSQL as primary database](0004-use-postgresql.md)
- [0005 - Use Docker for local development and deployment](0005-use-docker-for-deployment.md)
- [0006 - Manage secrets outside tracked configuration](0006-secrets-via-env-file.md)
- [0008 - Use signed webhooks for OpenMRS integration](0008-webhook-for-openmrs-integration.md)
- [0009 - Use MassTransit for asynchronous messaging](0009-masstransit-for-async-messaging.md)
- [0010 - Data retention and encryption policy](0010-data-retention-and-encryption-policy.md)
- [0011 - Use a layered folder structure](0011-layered-folder-structure.md)
- [0012 - Signed OpenMRS webhook contract](0012-signed-openmrs-webhook-contract.md)
- [0013 - Pragmatic automated test strategy](0013-pragmatic-automated-test-strategy.md)
- [0014 - CI quality gates](0014-ci-quality-gates.md)
- [0015 - Encryption and webhook security posture](0015-encryption-and-webhook-security-posture.md)
- [0016 - Observability via OpenTelemetry and Prometheus](0016-observability-stack.md)
- [0017 - Asynchronous messaging as a separate component](0017-async-messaging-as-separate-component.md)
- [0018 - Multi-OpenMRS hospital configuration](0018-multi-openmrs-hospital-configuration.md)
- [0019 - Durable RabbitMQ transport with PostgreSQL retry ledger](0019-durable-rabbitmq-postgresql-retry-ledger.md)
- [0020 - Use EF Core for application persistence](0020-use-ef-core-for-persistence.md)

## Superseded Decisions

- [0002 - Use Dapper as ORM](0002-use-dapper-as-orm.md), superseded by ADR 0020.
