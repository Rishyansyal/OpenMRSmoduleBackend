# C4 Level 2 — Containers

De technische bouwstenen van de OpenMRS Communicatiemodule.

```mermaid
flowchart TB
    classDef person fill:#08427b,stroke:#073b6f,color:#fff
    classDef container fill:#1168bd,stroke:#0b4884,color:#fff
    classDef db fill:#1168bd,stroke:#0b4884,color:#fff
    classDef ext fill:#999999,stroke:#6b6b6b,color:#fff

    zorg["👤 <b>Zorgmedewerker</b><br/><i>[Person]</i><br/>Gebruikt OpenMRS"]:::person
    openmrs["<b>OpenMRS EMR</b><br/><i>[Extern systeem]</i><br/>FHIR R4 + webhooks<br/>:3032"]:::ext
    providers["<b>Messaging Providers</b><br/><i>[Extern systeem]</i><br/>SwiftSend / SecurePost /<br/>LegacyLink / AsyncFlow"]:::ext

    subgraph module["OpenMRS Communicatiemodule"]
        direction TB
        api["<b>Backend API</b><br/><i>[Container: ASP.NET Core 10]</i><br/>REST + JWT + signed webhook,<br/>herinneringen, retentie — :5111"]:::container
        db[("<b>Database</b><br/><i>[Container: PostgreSQL 17]</i><br/>Users, message_logs,<br/>reminder_logs — :5432")]:::db
        bus["<b>Message Bus</b><br/><i>[Container: MassTransit]</i><br/>SendReminderCommand<br/>in-memory (dev) / RabbitMQ"]:::container

        api -->|"Leest en schrijft<br/>[SQL / TCP]"| db
        api <-->|"Publish / consume<br/>SendReminderCommand"| bus
    end

    zorg -->|"Gebruikt<br/>[HTTPS / :3032]"| openmrs
    openmrs -->|"Signed appointment webhook<br/>[HTTPS / :5111]"| api
    api -->|"FHIR R4 patiëntcontact<br/>[HTTPS / :3032]"| openmrs
    api -->|"Verstuurt berichten<br/>[REST + SOAP / HTTPS]"| providers
```
