# 16. Observability via OpenTelemetry and Prometheus

Date: 2026-05-25
Status: Accepted, amended 2026-05-30

## Context

Reminder delivery needs operational visibility without logging patient data.
The backend needs traces, metrics, and structured logs without requiring a
hosted observability vendor.

## Decision

Use OpenTelemetry instrumentation and a Prometheus scraping endpoint:

- ASP.NET Core, HTTP client, EF Core, and MassTransit tracing;
- `/metrics` Prometheus endpoint;
- structured stdout logs without patient names, contact details, or message
  content;
- `OpenMRS.Messaging` business metrics:
  - `messaging.messages_sent`;
  - `messaging.reminders_sent`;
  - `messaging.send_duration_ms`;
  - `data_retention.records_deleted`.

## Consequences

- Operators can monitor provider latency, reminder results, and retention runs.
- `/metrics` must be restricted by the deployment reverse proxy outside trusted
  networks.
- Production may add an OTLP exporter or host-level log collector without
  changing the domain workflow.
