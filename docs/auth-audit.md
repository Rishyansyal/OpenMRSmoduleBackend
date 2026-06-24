# Authentication and Authorization Audit

**Updated:** 2026-05-30

## Authentication Model

- ASP.NET Core Identity stores users and hashes passwords using its configured password hasher.
- `POST /auth/login` issues JWT bearer tokens after Identity lockout checks.
- Startup seeds an administrator from `Admin:Email` and `Admin:Password`.
- `POST /auth/register` returns `403 PUBLIC_REGISTRATION_DISABLED` unless
  `Admin:AllowPublicRegistration=true` is explicitly configured.
- The global fallback policy requires authenticated users unless an endpoint is
  explicitly marked `[AllowAnonymous]`.

## Authorization Model

The `Admin` role is required for operational actions:

| Endpoint | Required authorization |
|---|---|
| `POST /api/reminders/trigger` | `Admin` policy |
| `GET /api/reminders/history` | `Admin` policy |
| `GET /api/reminders/scheduled` | `Admin` policy |
| `GET /api/reminders/dead-lettered` | `Admin` policy |
| `GET /api/reminders/templates` | `Admin` policy |
| `POST /api/data-retention/trigger` | `Admin` policy |

Direct message history and AsyncFlow tracking remain scoped to the authenticated
sender. OpenMRS proxy calls require a configured organization and reject unknown
or disabled organizations.

## Webhook Security

`POST /api/webhooks/openmrs/appointments` is anonymous at the JWT layer because
OpenMRS authenticates with HMAC headers. Validation is organization-aware:

- unknown or disabled organizations are rejected;
- timestamp skew is bounded;
- the signature covers the timestamp and raw body;
- body size is limited to 256 KiB;
- payload fields are validated before processing;
- duplicate event IDs are handled idempotently.

## Remaining Operational Requirements

- Terminate HTTPS at the deployment boundary.
- Use unique credentials and webhook secrets for every hospital.
- Keep PostgreSQL and RabbitMQ off public interfaces.
- Restrict Swagger and metrics at the reverse proxy outside trusted networks.
- Complete the Docker runtime acceptance checks described in
  [testing.md](testing.md) before production rollout.
