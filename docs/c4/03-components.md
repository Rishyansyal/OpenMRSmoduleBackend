# C4 Level 3 — Componenten (Backend API)

Interne structuur van de ASP.NET Core backend, opgesplitst in twee views voor leesbaarheid.

## 3a · Synchrone request-flow

Inkomende HTTP-requests van API-clients en de OpenMRS-webhook.

```mermaid
flowchart LR
    classDef ext fill:#999999,stroke:#6b6b6b,color:#fff
    classDef db fill:#1168bd,stroke:#0b4884,color:#fff
    classDef component fill:#85bbf0,stroke:#5d82a8,color:#000
    classDef layerApi fill:#fef3c7,stroke:#a16207,color:#000
    classDef layerApp fill:#e0e7ff,stroke:#4338ca,color:#000
    classDef layerInfra fill:#dcfce7,stroke:#166534,color:#000

    client["<b>API Client</b><br/>[Swagger / REST]"]:::ext
    openmrs["<b>OpenMRS</b><br/>[FHIR + webhook]"]:::ext
    db[("<b>PostgreSQL</b>")]:::db

    subgraph apiLayer["Api (Controllers)"]
        direction TB
        authCtrl["AuthController<br/>/auth/*"]:::component
        msgCtrl["MessagesController<br/>/api/messages/*"]:::component
        openmrsCtrl["OpenMrsController<br/>/api/openmrs/*"]:::component
        webhookCtrl["OpenMrsWebhooksController<br/>/api/webhooks/openmrs/*"]:::component
        reminderCtrl["RemindersController<br/>/api/reminders/*"]:::component
    end

    subgraph appLayer["Application (Interfaces)"]
        direction TB
        authSvc["IAuthService"]:::component
        msgSvc["IMessagingService"]:::component
        openMrsSvc["IOpenMrsService"]:::component
        webhookSvc["IOpenMrsWebhookService"]:::component
        msgLog["IMessageLogRepository"]:::component
    end

    subgraph infraLayer["Infrastructure (Implementaties)"]
        direction TB
        openMrsImpl["OpenMrsService<br/>FHIR R4 client"]:::component
        webhookImpl["OpenMrsWebhookService<br/>EF + AES-GCM"]:::component
        signature["WebhookSignatureValidator<br/>HMAC-SHA256"]:::component
        providers2["Provider implementaties<br/>SwiftSend / SecurePost /<br/>LegacyLink / AsyncFlow"]:::component
        efCtx["ApplicationDbContext<br/>EF Core"]:::component
    end

    class apiLayer layerApi
    class appLayer layerApp
    class infraLayer layerInfra

    client -->|JWT auth| authCtrl
    client -->|Berichten beheren| msgCtrl
    client -->|Patiënten ophalen| openmrsCtrl
    client -->|Herinneringen| reminderCtrl
    openmrs -->|Signed webhook| webhookCtrl

    authCtrl --> authSvc
    msgCtrl --> msgSvc
    msgCtrl --> msgLog
    openmrsCtrl --> openMrsSvc
    webhookCtrl --> signature
    webhookCtrl --> webhookSvc

    msgSvc --> providers2
    openMrsSvc --> openMrsImpl
    webhookSvc --> webhookImpl
    msgLog --> efCtx
    webhookImpl --> efCtx
    efCtx -->|SQL| db
    openMrsImpl -->|FHIR R4| openmrs
```

## 3b · Asynchrone background-flow

Workers, message bus en consumers — los van inkomende requests.

```mermaid
flowchart LR
    classDef ext fill:#999999,stroke:#6b6b6b,color:#fff
    classDef db fill:#1168bd,stroke:#0b4884,color:#fff
    classDef component fill:#85bbf0,stroke:#5d82a8,color:#000
    classDef layerApp fill:#e0e7ff,stroke:#4338ca,color:#000
    classDef layerInfra fill:#dcfce7,stroke:#166534,color:#000

    db[("<b>PostgreSQL</b>")]:::db
    bus["<b>Message Bus</b><br/>[MassTransit]"]:::ext
    openmrs["<b>OpenMRS</b><br/>[FHIR]"]:::ext
    providers["<b>Messaging Providers</b>"]:::ext

    subgraph appLayer["Application (Interfaces)"]
        direction TB
        msgSvc["IMessagingService"]:::component
        openMrsSvc["IOpenMrsService"]:::component
        reminderLog["IReminderLogRepository"]:::component
        retentionSvc["IDataRetentionService"]:::component
    end

    subgraph infraLayer["Infrastructure (Workers + Impl)"]
        direction TB
        reminderWorker["ReminderWorker<br/>IHostedService — elke 5 min"]:::component
        consumer["SendReminderConsumer<br/>MassTransit IConsumer"]:::component
        retentionWorker["DataRetentionWorker<br/>IHostedService — elke 24u"]:::component
        metrics["MessagingMetrics<br/>OpenTelemetry"]:::component
        efCtx["ApplicationDbContext<br/>EF Core"]:::component
        openMrsImpl["OpenMrsService"]:::component
        providers2["Provider implementaties"]:::component
    end

    class appLayer layerApp
    class infraLayer layerInfra

    reminderWorker -->|Claim due reminders| efCtx
    reminderWorker -->|Publish SendReminderCommand| bus
    bus -->|Levert command| consumer
    consumer --> openMrsSvc
    consumer --> msgSvc
    consumer --> reminderLog
    consumer --> metrics

    retentionWorker --> retentionSvc
    retentionSvc -->|Bulk delete| efCtx

    msgSvc --> providers2
    openMrsSvc --> openMrsImpl
    reminderLog --> efCtx

    efCtx -->|SQL| db
    providers2 --> providers
    openMrsImpl -->|FHIR R4| openmrs
```
