# C4 Architecture Model

This folder contains the C4 model for the OpenMRS Appointment Reminder Platform:

- the OpenMRS O3 reference application distro in `../2.4-LU1-openMRS-Avans`;
- the custom OpenMRS Appointment Webhook Module inside that distro;
- the ASP.NET Core communication backend in this repository.

The previous custom Next.js frontend is out of scope. Clinicians work in OpenMRS O3. The communication backend receives signed appointment events, schedules 24-hour and 1-hour reminders, sends them through the configured provider for the organization, and keeps durable retry/audit state.

## C4 Scope

| Level | Diagram | SVG source | Status |
|---|---|---|---|
| 1 - System context | [01-context.md](01-context.md) | [01-context.svg](diagrams/01-context.svg) | Required C4 view |
| 2 - Containers | [02-containers.md](02-containers.md) | [02-containers.svg](diagrams/02-containers.svg) | Required C4 view |
| 3 - Components | [03-backend-components.md](03-backend-components.md) | [03-backend-components.svg](diagrams/03-backend-components.svg) | Backend API/worker container |
| 3 - Components | [04-openmrs-webhook-module-components.md](04-openmrs-webhook-module-components.md) | [04-openmrs-webhook-module-components.svg](diagrams/04-openmrs-webhook-module-components.svg) | OpenMRS backend custom module |
| Supporting - Dynamic | [05-appointment-webhook-dynamic.md](05-appointment-webhook-dynamic.md) | [05-appointment-webhook-dynamic.svg](diagrams/05-appointment-webhook-dynamic.svg) | Signed webhook and scheduling flow |
| Supporting - Dynamic | [06-reminder-delivery-dynamic.md](06-reminder-delivery-dynamic.md) | [06-reminder-delivery-dynamic.svg](diagrams/06-reminder-delivery-dynamic.svg) | Due reminder delivery and retry flow |
| Supporting - System landscape | [07-multi-hospital-landscape.md](07-multi-hospital-landscape.md) | [07-multi-hospital-landscape.svg](diagrams/07-multi-hospital-landscape.svg) | Multi-OpenMRS integration |
| Supporting - Deployment | [08-local-deployment.md](08-local-deployment.md) | [08-local-deployment.svg](diagrams/08-local-deployment.svg) | Local Docker environment |
| Supporting - Deployment | [09-production-deployment.md](09-production-deployment.md) | [09-production-deployment.svg](diagrams/09-production-deployment.svg) | Production/staging target |

Level 4 code diagrams are intentionally omitted from long-lived documentation. The official C4 guidance marks code diagrams as optional and not recommended for most long-lived docs because IDEs and UML tools can generate them on demand.

## Notation

The SVG diagrams are generated from [generate-c4-diagrams.mjs](generate-c4-diagrams.mjs), which applies the visual notation used in the c4model.com examples:

- filled green boxes for in-scope software systems, containers, components, queues, and databases;
- filled green person silhouettes;
- filled grey boxes for external software systems;
- dashed grey software-system and deployment-node boundaries;
- dashed, labelled one-way relationships with protocol/technology labels in brackets;
- explicit element type and technology text inside the element where relevant.

Every diagram page includes a diagram key. The key defines:

- element type: Person, Software System, Container, Component, Database, Queue, Deployment Node, or Infrastructure Node;
- ownership: elements inside the platform boundary are part of this solution; `_Ext` elements are external dependencies;
- relationships: arrows are unidirectional and labelled with intent; container-to-container relationships include the relevant protocol or technology;
- icons/logos: no product logos or custom icons are used, so there are no hidden icon semantics to interpret.

## Regenerate SVG Diagrams

```powershell
node .\generate-c4-diagrams.mjs
```
