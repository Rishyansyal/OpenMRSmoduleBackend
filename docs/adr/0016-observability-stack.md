# 16. Observability via OpenTelemetry en Prometheus

Date: 2026-05-25

## Status

Accepted

## Context

NFE-9 in [requirements.md](../requirements.md) eist *observability*: zonder zichtbaarheid op het draaiende systeem kunnen we niet onderbouwen dat reminders binnen 24h/1h voor de afspraak echt verzonden worden, kunnen we provider-failures niet vroeg detecteren, en hebben we geen bewijsmateriaal voor performance- en betrouwbaarheidsclaims (zie ook de FMEA in [fmea.md](../fmea.md) — vrijwel elke mitigatie verwijst naar een metric of trace).

Drie soorten signalen zijn nodig:

1. **Traces** — request → controller → service → DB → provider, inclusief MassTransit publish/consume. Nodig om te zien waarom een reminder traag of niet verstuurd is.
2. **Metrics** — verzendaantallen per provider, latency-histogrammen, retentie-runs, HTTP-statuscodes. Nodig voor SLO-bewaking en capaciteitsplanning.
3. **Logs** — gestructureerd, zonder PII (zie [ADR-0015](0015-encryption-and-webhook-security-posture.md)). Voor incident-onderzoek.

Tegelijk willen we **geen vendor-lock-in** (Avans-project, korte looptijd) en **geen extra hosted kosten** voor lokale dev en CI.

## Decision

We gebruiken **OpenTelemetry** als instrumentatie-API en **Prometheus** als metrics-backend, met de volgende invulling:

- **SDK:** de `OpenTelemetry.Extensions.Hosting` pakketten in `Program.cs` (regels 209–224). Eén pipeline, zowel tracing als metrics.
- **Tracing-bronnen:**
  - `AddAspNetCoreInstrumentation()` — inkomende HTTP requests + statuscodes + latency
  - `AddHttpClientInstrumentation()` — uitgaande calls naar FHIR/providers/FakeComWorld
  - `AddEntityFrameworkCoreInstrumentation()` — SQL queries inclusief duur
  - `AddSource("MassTransit")` — publish/consume spans van de message bus
- **Metrics:**
  - ASP.NET Core + HttpClient default metrics
  - Eigen `Meter` `OpenMRS.Messaging` in [`MessagingMetrics`](../../Infrastructure/Observability/MessagingMetrics.cs) met vier business-metrics:
    - `messaging.messages_sent` (counter, labels: provider, type, success)
    - `messaging.reminders_sent` (counter, labels: window, success)
    - `messaging.send_duration_ms` (histogram, label: provider)
    - `data_retention.records_deleted` (counter, label: table)
- **Exporter:** `AddPrometheusExporter()` met `UseOpenTelemetryPrometheusScrapingEndpoint()` op intern pad `/metrics`. Geen push naar een hosted backend; Prometheus scrapet pull-based.
- **Logging:** Microsoft.Extensions.Logging met structured logs naar stdout. In productie wordt stdout door de container-host gecollecteerd (bv. Loki, CloudWatch). Geen patiëntnaam, telefoon, e-mail of berichtinhoud — afgedwongen door code review en gerichte unit tests.

### Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **Azure Application Insights** | Vendor-lock-in op Azure; data-residency en kosten lastig in een onderwijsproject. OpenTelemetry kan later alsnog naar AppInsights exporten zonder code-wijziging. |
| **Datadog / New Relic** | Commercieel, kostenmodel onverenigbaar met short-lived project; vereist agent of API-key in elke deployment. |
| **Elastic / ELK-stack** | Log-centric; zwaargewicht (Elasticsearch + Logstash + Kibana). Disproportioneel voor onze schaal. |
| **Serilog stand-alone naar bestand** | Alleen logs, geen metrics of traces. Voldoet niet aan NFE-9 dat ook performance-zichtbaarheid eist. |
| **Custom `IMetricsCollector` interface** | Niet keer-uitwisselbaar; we zouden zelf retention, aggregatie en exposition moeten bouwen. OpenTelemetry is dé open standaard hiervoor. |

Het patroon **OpenTelemetry SDK + Prometheus pull-exporter** is bewust gekozen omdat het draagbaar is: dezelfde instrumentatie kan in productie via een OTLP-exporter naar een commerciële backend gestuurd worden zonder de code aan te raken.

## Consequences

**Voordelen:**

- Eén API (`Meter`, `ActivitySource`) voor metrics én tracing — geen dubbele instrumentatie.
- `/metrics`-endpoint is direct compatibel met elke Prometheus-installatie en met Grafana-dashboards.
- Business-metrics in `MessagingMetrics` zijn typesafe en herbruikbaar in `SendReminderConsumer`, `DataRetentionService` en toekomstige consumers.
- Tracing dekt automatisch de hele request-keten (HTTP → DB → MassTransit → FHIR) zonder extra code per controller.

**Nadelen / aandachtspunten:**

- `/metrics` mag niet publiek bereikbaar zijn — bevat operationele details. In dev wordt het endpoint alleen op `127.0.0.1:5111` aangeboden ([ADR-0005](0005-use-docker-for-deployment.md)); in productie zit het achter de reverse proxy met een ACL of pad-allowlist.
- Prometheus is **pull-based** — bij hosts achter NAT/firewall is een Prometheus-pushgateway of een OTLP-exporter naar een hosted endpoint nodig. Dit is een productiebeslissing, geen code-wijziging.
- Histograms (`send_duration_ms`) hebben default-buckets; voor SLO's op p95/p99 latency moeten we mogelijk custom buckets configureren wanneer we werkelijke load-test-data hebben (zie [performance-report.md](../performance-report.md) zodra deze er is).
- Logs blijven stdout-only. Voor centrale aggregatie in productie is een log-collector aan host-zijde nodig; dat valt buiten deze ADR.
- Sampling staat nu op "altijd aan" (100% traces). Bij hoge load moet hoofd-sampling worden ingesteld om opslag- en exportkosten te beheersen — een toekomstige ADR.
