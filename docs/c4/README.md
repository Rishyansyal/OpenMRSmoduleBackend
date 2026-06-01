# C4 Architecture Diagrams

These diagrams describe the backend and OpenMRS O3 architecture only. The previous custom Next.js frontend is no longer part of the system.

Mermaid sources live in [diagrams/](diagrams/). PNG exports, when generated, live in [images/](images/).

| Diagram | Markdown | Mermaid source | PNG |
|---|---|---|---|
| System context | [01-context.md](01-context.md) | [01-context.mmd](diagrams/01-context.mmd) | [01-context.png](images/01-context.png) |
| Containers | [02-containers.md](02-containers.md) | [02-containers.mmd](diagrams/02-containers.mmd) | [02-containers.png](images/02-containers.png) |
| Durable delivery and retry | [10-durable-delivery-retry.md](10-durable-delivery-retry.md) | [10-durable-delivery-retry.mmd](diagrams/10-durable-delivery-retry.mmd) | [10-durable-delivery-retry.png](images/10-durable-delivery-retry.png) |
| Multi-OpenMRS configuration | [11-multi-openmrs-config.md](11-multi-openmrs-config.md) | [11-multi-openmrs-config.mmd](diagrams/11-multi-openmrs-config.mmd) | [11-multi-openmrs-config.png](images/11-multi-openmrs-config.png) |
| Webhook authentication flow | [12-webhook-auth-flow.md](12-webhook-auth-flow.md) | [12-webhook-auth-flow.mmd](diagrams/12-webhook-auth-flow.mmd) | [12-webhook-auth-flow.png](images/12-webhook-auth-flow.png) |
| Deployment | [09-deployment.md](09-deployment.md) | [09-deployment-1.mmd](diagrams/09-deployment-1.mmd), [09-deployment-2.mmd](diagrams/09-deployment-2.mmd) | [dev](images/09-deployment-1.png), [prod](images/09-deployment-2.png) |
| ER model | [08-er-diagram.md](08-er-diagram.md) | [08-er-diagram.mmd](diagrams/08-er-diagram.mmd) | [08-er-diagram.png](images/08-er-diagram.png) |

Additional current views:

- [Backend components](03-components.md)
- [Reminder process](04-reminder-process.md)
- [Core class relationships](05-class-diagram.md)
- [Use cases](06-use-case.md)
- [API and OpenMRS O3 flows](07-user-flow.md)

## Regenerate PNG Exports

From `OpenMRSmoduleBackend/docs/c4`:

```powershell
$files = @(
  "01-context",
  "02-containers",
  "10-durable-delivery-retry",
  "11-multi-openmrs-config",
  "12-webhook-auth-flow",
  "09-deployment-1",
  "09-deployment-2",
  "08-er-diagram"
)

foreach ($name in $files) {
  npx -y @mermaid-js/mermaid-cli -i "diagrams/$name.mmd" -o "images/$name.png" -w 2400 -b white
}
```

If `mmdc` is installed globally, replace `npx -y @mermaid-js/mermaid-cli` with `mmdc`.

When Mermaid CLI is unavailable, render through Kroki:

```powershell
$ErrorActionPreference = "Stop"
Get-ChildItem "diagrams/*.mmd" | ForEach-Object {
  $out = Join-Path "images" ($_.BaseName + ".png")
  Invoke-WebRequest -Uri "https://kroki.io/mermaid/png" `
    -Method Post `
    -ContentType "text/plain" `
    -Body ([System.IO.File]::ReadAllText($_.FullName)) `
    -OutFile $out
}
```

The PNG exports in this directory were regenerated from the checked-in Mermaid sources on 2026-05-30 using the Kroki command.
