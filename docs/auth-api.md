# Authentication API

This document used to describe custom frontend integration. That frontend is no longer part of the architecture. The current supported consumers are OpenMRS O3 integration points, direct API clients, Swagger during development, and operational scripts.

## Login

```text
POST /auth/login
```

Request:

```json
{
  "email": "admin@example.test",
  "password": "<password>"
}
```

Response:

```json
{
  "token": "eyJhbGci...",
  "expiresAt": "2026-05-29T12:00:00Z"
}
```

Use the token on protected endpoints:

```http
Authorization: Bearer eyJhbGci...
```

## Admin Bootstrap And Registration

The backend bootstraps an admin account when `Admin:Email` and `Admin:Password` are configured.

`POST /auth/register` is disabled by default and returns:

```json
{
  "error": "PUBLIC_REGISTRATION_DISABLED",
  "message": "Public registration is disabled. Use the bootstrapped admin account."
}
```

Enable public registration only with `Admin:AllowPublicRegistration=true`.

## Current Auth Endpoints

| Endpoint | Auth | Purpose |
|---|---|---|
| `POST /auth/login` | anonymous | Exchange admin/user credentials for JWT. |
| `POST /auth/register` | anonymous, disabled unless configured | Optional self-registration. |
| `GET /auth/me` | JWT | Return current user id and email. |
