# API And OpenMRS O3 Flows

The custom web frontend has been removed. Clinicians use OpenMRS O3 for clinical workflows. Backend interactions are OpenMRS webhooks, FHIR calls, Swagger/API clients, and admin endpoints.

## Clinician Appointment Flow

```mermaid
flowchart TD
    classDef actor fill:#d0ebff,stroke:#1971c2,color:#111
    classDef system fill:#d3f9d8,stroke:#2b8a3e,color:#111
    classDef action fill:#fff3bf,stroke:#f08c00,color:#111

    clinician["Clinician"]:::actor
    o3["OpenMRS O3"]:::system
    webhook["Signed appointment webhook"]:::action
    backend["Communication backend"]:::system
    reminder["Scheduled reminders"]:::action

    clinician -->|"creates/updates appointment"| o3
    o3 --> webhook
    webhook --> backend
    backend --> reminder
```

## Admin Auth Flow

```mermaid
flowchart TD
    classDef action fill:#d0ebff,stroke:#1971c2,color:#111
    classDef decision fill:#fff3bf,stroke:#f08c00,color:#111
    classDef done fill:#d3f9d8,stroke:#2b8a3e,color:#111
    classDef error fill:#ffe3e3,stroke:#c92a2a,color:#111

    seed["Startup seeds admin from Admin:Email/Admin:Password"]:::action
    login["POST /auth/login"]:::action
    valid{"credentials valid?"}:::decision
    token["JWT returned"]:::done
    reject["401 Unauthorized"]:::error
    register["POST /auth/register"]:::action
    public{"AllowPublicRegistration?"}:::decision
    forbid["403 PUBLIC_REGISTRATION_DISABLED"]:::error

    seed --> login
    login --> valid
    valid -->|yes| token
    valid -->|no| reject
    register --> public
    public -->|no| forbid
    public -->|yes| token
```

## Direct Message API Flow

```mermaid
flowchart TD
    classDef action fill:#d0ebff,stroke:#1971c2,color:#111
    classDef decision fill:#fff3bf,stroke:#f08c00,color:#111
    classDef done fill:#d3f9d8,stroke:#2b8a3e,color:#111
    classDef error fill:#ffe3e3,stroke:#c92a2a,color:#111

    client["Swagger/API client with JWT"]:::action
    providers["GET /api/messages/providers"]:::action
    send["POST /api/messages with explicit provider"]:::action
    ok{"provider send success?"}:::decision
    log["message_logs row"]:::done
    fail["error returned/logged; no provider fallback"]:::error

    client --> providers --> send --> ok
    ok -->|yes| log
    ok -->|no| fail
```
