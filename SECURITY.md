# Security Policy

## Scope

This backend handles OpenMRS appointment metadata, encrypted patient references, provider credentials, and reminder delivery state. Treat vulnerabilities affecting authentication, authorization, webhook validation, encryption, retention, or message delivery as security issues.

## Reporting

Report vulnerabilities privately to the deployment owner or repository maintainer. Do not include real patient data, credentials, webhook secrets, or production URLs in issue trackers.

Include:

- affected endpoint or component,
- reproduction steps using synthetic data,
- expected and actual behavior,
- suggested severity,
- relevant logs with secrets and patient data removed.

## Deployment Baseline

- Run behind HTTPS.
- Use unique JWT, encryption, webhook, database, RabbitMQ, OpenMRS, and provider credentials.
- Disable public registration unless explicitly required.
- Restrict Swagger and metrics exposure at the reverse proxy when deployed outside a trusted network.
- Keep RabbitMQ and PostgreSQL off public interfaces.
