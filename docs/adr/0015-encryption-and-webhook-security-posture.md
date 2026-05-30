# 15. Encryption and webhook security posture

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

Webhook events and hospital configuration contain patient context and
credentials. The backend must avoid tracked secrets, plaintext sensitive fields,
and unauthenticated OpenMRS requests.

## Decision

- Use AES-256-GCM application-level encryption.
- Configure separate 32-byte keys through `SECURITY_ENCRYPTION_KEY` and
  `ENCRYPTION_KEY`.
- Encrypt appointment context, OpenMRS credentials, webhook secrets, provider
  credentials, and sensitive reminder lookup values before persistence.
- Authenticate OpenMRS webhooks using per-organization HMAC-SHA256 signatures
  with timestamp skew checks.
- Terminate TLS at the deployment boundary and keep database and broker ports
  private.
- Keep logs free of patient names, contact details, and message content.

## Consequences

- Deployments must provide encryption keys through secret storage.
- Key rotation remains an explicit operational procedure.
- A database leak does not directly expose stored credentials or patient
  context.
