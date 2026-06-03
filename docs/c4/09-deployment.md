# Deployment

The deployment keeps the backend, PostgreSQL, and message broker separate from OpenMRS O3. OpenMRS remains the clinical UI and EMR. The backend is not paired with a custom Next.js frontend.

## Local Development

```mermaid
flowchart TB
    classDef host fill:#f8f9fa,stroke:#495057,color:#111
    classDef container fill:#d0ebff,stroke:#1971c2,color:#111
    classDef db fill:#d3f9d8,stroke:#2b8a3e,color:#111
    classDef queue fill:#e5dbff,stroke:#5f3dc4,color:#111
    classDef ext fill:#ffe8cc,stroke:#d9480f,color:#111

    subgraph host["Developer machine"]
        browser["Browser<br/>OpenMRS O3"]:::ext
        fake["FakeComWorld<br/>localhost:1337"]:::ext
        openmrs["OpenMRS O3 compose<br/>gateway :3032, backend, frontend, MariaDB"]:::container
        api["OpenMRSmoduleBackend api<br/>localhost:5111"]:::container
        pg[("PostgreSQL<br/>backend network only")]:::db
        bus["RabbitMQ / MassTransit<br/>required outside IntegrationTest"]:::queue
    end

    browser --> openmrs
    openmrs -->|"signed webhook"| api
    api -->|"FHIR R4"| openmrs
    api -->|"SQL"| pg
    api -->|"publish/consume"| bus
    api -->|"provider API"| fake
```

## Production Target

```mermaid
flowchart TB
    classDef edge fill:#d0ebff,stroke:#1971c2,color:#111
    classDef app fill:#d3f9d8,stroke:#2b8a3e,color:#111
    classDef db fill:#ffe3e3,stroke:#c92a2a,color:#111
    classDef queue fill:#e5dbff,stroke:#5f3dc4,color:#111
    classDef ext fill:#f1f3f5,stroke:#495057,color:#111

    clinician["Clinician"]:::ext
    openmrs["OpenMRS O3 deployments<br/>one or more hospitals"]:::ext
    providers["Messaging providers"]:::ext

    subgraph edge["Edge"]
        proxy["Reverse proxy<br/>TLS termination, HSTS, routing"]:::edge
    end

    subgraph app["Application tier"]
        api1["API pod<br/>controllers, workers, consumers"]:::app
        api2["API pod<br/>optional horizontal scale"]:::app
        rabbit["RabbitMQ<br/>durable queues, retries, dead letters"]:::queue
    end

    subgraph data["Data tier"]
        pg[("PostgreSQL<br/>encrypted config, appointment state, retry ledger, audit logs")]:::db
    end

    prometheus["Prometheus<br/>scrapes /metrics"]:::ext

    clinician -->|"uses"| openmrs
    openmrs -->|"signed webhooks HTTPS"| proxy
    proxy --> api1
    proxy --> api2
    api1 --> pg
    api2 --> pg
    api1 <--> rabbit
    api2 <--> rabbit
    api1 -->|"FHIR R4"| openmrs
    api2 -->|"FHIR R4"| openmrs
    api1 --> providers
    api2 --> providers
    prometheus --> api1
    prometheus --> api2
```

## Notes

- RabbitMQ is required for development, staging, and production delivery. Only `IntegrationTest` uses in-memory MassTransit.
- PostgreSQL is the source of truth for retry state; RabbitMQ is the transport, not the business ledger.
- Multiple OpenMRS O3 deployments are separated by organization id and per-org secrets.
- Provider fallback is not automatic. Operational retry targets the same configured provider.
