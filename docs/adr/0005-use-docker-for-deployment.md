# 5. Use Docker for local dev and deployment

Date: 2026-05-23

## Status

Accepted

## Context

Acceptatiecriterium voor de backend: "De API start bij iedereen via docker en lokaal." We willen "works on my machine" voorkomen en hebben een reproduceerbare manier nodig om de app + database op te starten.

## Decision

We leveren een **multi-stage [Dockerfile](../../Dockerfile)** (sdk → publish → aspnet runtime) en een **[docker-compose.yml](../../docker-compose.yml)** met twee services: `api` en `db` (PostgreSQL).

Configuratie via environment variables, geladen uit een lokale `.env` (zie [ADR 0006](0006-secrets-via-env-file.md)). De app leest `ConnectionStrings__DefaultConnection` uit de env, zodat dezelfde image lokaal én in productie werkt.

## Overwogen alternatieven

| Alternatief | Reden van afval |
|---|---|
| **Bare-metal install (Postgres + .NET SDK lokaal)** | "Works on my machine"-risico; versie-drift tussen developers; tijdrovende onboarding voor nieuwe teamleden. |
| **Vagrant + VirtualBox** | Veel zwaarder dan Docker (volledige VM), niet meer mainstream voor .NET-projecten. |
| **VS Code devcontainers** | Bouwt zelf op Docker, geeft alleen een editor-laag erbovenop. We krijgen geen extra reproduceerbaarheid t.o.v. plain Docker Compose; wel een editor-lock-in. |
| **Nix / NixOS** | Reproduceerbaarder dan Docker, maar steile leercurve en geen team-ervaring. |
| **Cloud dev environments (GitHub Codespaces, Gitpod)** | Vereist account + kosten; overbodig nu lokale Docker volstaat. |

## Consequences

- Iedereen runt `docker compose up` en heeft een werkende stack — geen aparte Postgres-install nodig.
- Image is reproduceerbaar; CI kan dezelfde image bouwen en pushen.
- Lokaal `dotnet run` blijft mogelijk, maar vereist dat de developer zelf een Postgres draait én `ConnectionStrings__DefaultConnection` set in zijn shell of `.env`.
- HTTPS in development gebeurt niet in de container (port 8080, http only). Voor productie zit TLS-termination op de reverse proxy / load balancer.
