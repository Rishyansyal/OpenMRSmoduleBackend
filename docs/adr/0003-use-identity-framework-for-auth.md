# 3. Use ASP.NET Core Identity for authentication

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

The backend needs secure password hashing, lockout behavior, JWT issuance, and an admin bootstrap path. Authentication is consumed by direct API clients, Swagger, scripts, and backend integration workflows.

## Decision

Use ASP.NET Core Identity for user storage, password hashing, and lockout behavior, with a custom `AuthController` for the limited API surface:

- `POST /auth/login`
- `POST /auth/register`
- `GET /auth/me`

Do not expose `MapIdentityApi<IdentityUser>()`; it would publish management endpoints that are not needed for this backend. Startup creates the admin role/user from `Admin:Email` and `Admin:Password`. Public registration is disabled unless `Admin:AllowPublicRegistration=true`.

## Consequences

- Password hashing and lockout use framework defaults instead of custom cryptography.
- The public auth surface stays small.
- Operators can deploy a usable admin account without opening self-registration.
- Admin bootstrap credentials must be stored as secrets and rotated through the deployment platform.
