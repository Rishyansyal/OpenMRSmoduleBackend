# 17. Asynchroon messaging-component als apart, los te schalen deel van het systeem

Date: 2026-05-25

## Status

Accepted

## Context

Tijdens de workshop *Applicatie-integratie* is gevraagd om expliciet één architectonisch apart component te verantwoorden: welk deel van het systeem leeft op zichzelf, draait in een eigen proces/container, kan onafhankelijk schalen of falen, en waarom is die scheiding nodig in plaats van alles in één deployable te proppen.

Onze [containerview (C4 L2)](../c4/02-containers.md) toont drie containers: backend API, database en **message bus**. De eerste twee zijn standaard bouwstenen. Het vierde — de message bus + de bijbehorende worker/consumer-keten — is de niet-vanzelfsprekende keuze. Drie functionele eisen vragen om asynchrone verwerking die *niet* op een inkomend HTTP-request mag wachten:

1. **Reminders 24 u en 1 u vooraf** (FE-1). Een afspraak op donderdag 14:00 vereist een 24h-reminder op woensdag 14:00 — de webhook van OpenMRS komt nú binnen, het bericht moet straks weg. Synchroon afhandelen vanuit de webhookcontroller is onmogelijk.
2. **Provider-uitval / rate-limiting** (NFE-7). SwiftSend heeft 10/min limiet, SecurePost JWT-expiry, LegacyLink SOAP timeouts. Retries en backoff horen niet in een controller — een gefaalde provider mag de webhook niet 30 s laten hangen of een 5xx geven aan OpenMRS.
3. **Data-retentie** (NFE-10/11). Bulk-deletes op `appointment_notifications`, `reminder_logs`, `message_logs`, `webhook_event_logs` elke 24 u. Dit hoort niet bij een HTTP-handler.

Bovendien eist NFE-1 (zelfstandig SaaS-proces) dat we kunnen **horizontaal schalen**: meerdere API-instanties achter een load balancer, maar nog steeds maar één bericht per reminder — anders krijgt de patiënt dubbele SMS'jes.

## Decision

Het asynchrone messaging-deel is een **apart, los te schalen component**, bestaande uit drie onderdelen die samen één architectonische verantwoordelijkheid dragen ("doe werk dat niet aan een HTTP-request hangt"):

| Onderdeel | Implementatie | Rol |
|---|---|---|
| **Message bus** | RabbitMQ (prod, eigen container) / in-memory (dev) | Transport + duurzaamheid van commands |
| **Producer** | [`ReminderWorker`](../../Infrastructure/Reminders/ReminderWorker.cs) — `IHostedService`, elke 5 min | Claimt due `scheduled_reminders` en publiceert `SendReminderCommand` |
| **Consumer** | [`SendReminderConsumer`](../../Infrastructure/Messaging/Consumers/SendReminderConsumer.cs) — `IConsumer<SendReminderCommand>` | Idempotency-check, FHIR-call, provider-call, logging, metrics |

**MassTransit** is het abstraction layer dat producer en consumer over de bus laat praten ([ADR-0009](0009-masstransit-for-async-messaging.md) bevat de framework-keuze). Deze ADR gaat één laag dieper: *waarom* dit als apart component.

### Wat maakt het een "apart component"?

- **Eigen proces/container in productie.** RabbitMQ draait als losse pod (`:5672`), zie [deployment-diagram (prod)](../c4/09-deployment.md). De API kan opnieuw deployen zonder dat in-flight messages verloren gaan; de bus kan worden geüpgraded zonder de API-code te raken.
- **Eigen schaal-as.** Bij hoge load schalen we *consumers* (meer API-pods die `SendReminderConsumer` registreren), zonder de inkomende HTTP-laag te raken. Andersom: een spike op de webhook leidt niet tot een spike in provider-calls — die worden gebufferd op de bus.
- **Eigen failure-domein.** Provider-uitval blokkeert geen webhooks; webhook-spike crasht geen workers. Dead-letter queue isoleert giftige messages.
- **Eigen contract.** Het `SendReminderCommand`-record is een stabiel, immutable POCO. Toekomstige consumers (bv. patiënt-confirmatie, no-show analytics) abonneren zich op dezelfde messages zonder de producer aan te raken.

### Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **Synchroon in de webhook-controller versturen** | Tijdsverschil (24h/1h vooraf) onmogelijk; provider-latency lekt naar OpenMRS; rate-limits crashen de keten. |
| **`BackgroundService` zonder message bus, direct provider-call** | Geen retry-semantiek, geen dead-letter, geen horizontale scaling zonder dubbele berichten; één crash = werk verloren. |
| **Quartz.NET / Hangfire (scheduler-as-component)** | Wel goede scheduling, maar deze tools koppelen scheduling en uitvoering. We willen de **scheduling** in `scheduled_reminders` (DB als bron van waarheid) en alleen het **dispatch** via de bus. Hangfire/Quartz zou de DB als state-store én hun eigen abstractie introduceren — dubbel werk. |
| **Cloud-managed bus (Azure Service Bus / AWS SQS) als enige optie** | Vendor-lock-in; in dev werkt dat niet zonder cloud-account. MassTransit ondersteunt RabbitMQ én Service Bus, dus we kunnen later switchen zonder code-wijziging. Voor het project kiezen we self-hosted RabbitMQ. |
| **Separate microservice (eigen `.csproj`/repo voor de worker)** | Operationeel zwaarder (eigen build, eigen CI, eigen deploy). De `IHostedService` draait nu binnen het API-proces én kan in de toekomst eenvoudig afgesplitst worden — het is al geïsoleerd in `Infrastructure.Reminders` en gebruikt alleen interfaces uit `Application`. Splitsing is een **opex-beslissing**, niet een code-beslissing. |

De keuze is daarmee bewust **niet** "een aparte microservice", maar **wel** "een aparte logische component met eigen lifecycle, eigen schaal-as en eigen failure-domein, achter een echt message-broker proces". Dat is volgens onze schaal de juiste granulariteit.

## Consequences

**Voordelen:**

- HTTP-responses blijven snel — provider-latency en retries lekken niet in de webhook-keten.
- Reminders kunnen exact 24 u / 1 u vooraf worden verstuurd, los van wanneer de webhook binnenkwam.
- Horizontaal schalen werkt: meerdere consumer-instanties verdelen messages via de bus zonder dubbele afhandeling (RabbitMQ work-queue semantics + idempotency-check in [`SendReminderConsumer`](../../Infrastructure/Messaging/Consumers/SendReminderConsumer.cs)).
- Nieuwe asynchrone use cases (no-show analytics, patiënt-confirmatie) sluiten aan op dezelfde bus zonder bestaande code te raken.
- Failure-isolatie: provider down ≠ webhook down ≠ data-retentie geblokkeerd.

**Kosten / aandachtspunten:**

- In productie hebben we een extra te onderhouden component (RabbitMQ): backups, monitoring, credentialing. Geadresseerd via container-orchestration en de observability-stack ([ADR-0016](0016-observability-stack.md)).
- Dev gebruikt in-memory transport — niet representatief voor RabbitMQ-failure modes (message-ordering, persistence, dead-lettering). Smoke tests met RabbitMQ in staging blijven nodig.
- Elke consumer **moet** idempotent zijn. Afgedwongen door de unique-index `(encounter_id_hash, reminder_window)` op `reminder_logs` plus `AlreadySentAsync` in de consumer.
- Correlation-id en tracing lopen via OpenTelemetry's MassTransit-instrumentatie ([ADR-0016](0016-observability-stack.md)) zodat een request van webhook → bus → consumer in één trace zichtbaar is.

## Relatie tot andere ADR's

- [ADR-0008](0008-webhook-for-openmrs-integration.md) — webhooks leveren de events die deze component verwerkt.
- [ADR-0009](0009-masstransit-for-async-messaging.md) — framework-keuze; deze ADR (0017) verantwoordt het **als apart component**, ADR-0009 verantwoordt **MassTransit als framework**.
- [ADR-0010](0010-data-retention-and-encryption-policy.md) — retentie-worker leeft in hetzelfde component.
- [ADR-0016](0016-observability-stack.md) — leveren metrics en traces voor dit component.
