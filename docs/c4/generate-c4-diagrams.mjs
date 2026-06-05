import { mkdir, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const __dirname = dirname(fileURLToPath(import.meta.url));
const outDir = join(__dirname, "diagrams");

const personGreen = "#438d12";
const systemGreen = "#6cb33f";
const containerGreen = "#85bb57";
const componentGreen = "#c9efb5";
const externalGrey = "#999999";
const relationGrey = "#707070";
const black = "#333333";
const white = "#ffffff";

function esc(value) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function wrap(text, max = 32) {
  const words = String(text ?? "").split(/\s+/).filter(Boolean);
  const lines = [];
  let line = "";
  for (const word of words) {
    if (!line) {
      line = word;
    } else if ((line + " " + word).length <= max) {
      line += " " + word;
    } else {
      lines.push(line);
      line = word;
    }
  }
  if (line) lines.push(line);
  return lines;
}

function textBlock(lines, x, y, { size = 16, weight = "400", color = black, align = "middle", italic = false, gap = 18 } = {}) {
  const anchor = align === "left" ? "start" : align === "right" ? "end" : "middle";
  const style = `font-size:${size}px;font-weight:${weight};fill:${color};font-style:${italic ? "italic" : "normal"}`;
  return lines.map((line, index) =>
    `<text x="${x}" y="${y + index * gap}" text-anchor="${anchor}" style="${style}">${esc(line)}</text>`
  ).join("\n");
}

function marker() {
  return `
  <defs>
    <marker id="arrow" markerWidth="12" markerHeight="10" refX="11" refY="5" orient="auto" markerUnits="strokeWidth">
      <path d="M 0 0 L 12 5 L 0 10 z" fill="${relationGrey}" />
    </marker>
  </defs>`;
}

function nodeCenter(n) {
  return { x: n.x + n.w / 2, y: n.y + n.h / 2 };
}

function anchor(n, side) {
  switch (side) {
    case "top": return { x: n.x + n.w / 2, y: n.y };
    case "bottom": return { x: n.x + n.w / 2, y: n.y + n.h };
    case "left": return { x: n.x, y: n.y + n.h / 2 };
    case "right": return { x: n.x + n.w, y: n.y + n.h / 2 };
    default: return nodeCenter(n);
  }
}

function colourFor(kind) {
  if (kind === "person") return personGreen;
  if (kind === "system") return systemGreen;
  if (kind === "container") return containerGreen;
  if (kind === "component") return componentGreen;
  if (kind === "external" || kind === "external-red" || kind === "external-orange") return externalGrey;
  return containerGreen;
}

function drawDb(n, colour = containerGreen) {
  const top = n.y + 22;
  const bottom = n.y + n.h - 22;
  const rx = n.w / 2 - 8;
  const cx = n.x + n.w / 2;
  return `
    <path d="M ${n.x + 8} ${top} C ${n.x + 8} ${n.y + 4}, ${n.x + n.w - 8} ${n.y + 4}, ${n.x + n.w - 8} ${top}
             L ${n.x + n.w - 8} ${bottom}
             C ${n.x + n.w - 8} ${n.y + n.h - 4}, ${n.x + 8} ${n.y + n.h - 4}, ${n.x + 8} ${bottom}
             Z" fill="${colour}" stroke="${colour}" stroke-width="2"/>
    <ellipse cx="${cx}" cy="${top}" rx="${rx}" ry="18" fill="${colour}" stroke="${colour}" stroke-width="2"/>
    <ellipse cx="${cx}" cy="${top}" rx="${rx}" ry="18" fill="none" stroke="rgba(0,0,0,0.16)" stroke-width="1"/>`;
}

function drawPerson(n) {
  const colour = personGreen;
  const cx = n.x + n.w / 2;
  const headY = n.y + 34;
  const bodyY = n.y + 72;
  const bodyHeight = n.h - 72;
  return `
    <circle cx="${cx}" cy="${headY}" r="32" fill="${colour}" stroke="${colour}" stroke-width="2"/>
    <rect x="${n.x + 16}" y="${bodyY}" width="${n.w - 32}" height="${bodyHeight}" rx="42" fill="${colour}" stroke="${colour}" stroke-width="2"/>
    ${textBlock([n.name], cx, bodyY + 34, { size: 17, weight: "700", color: white })}
    ${textBlock([n.type ?? "[Person]"], cx, bodyY + 55, { size: 10, color: white, italic: true })}
    ${textBlock(wrap(n.description, 25).slice(0, 1), cx, bodyY + 76, { size: 11, color: white, gap: 14 })}`;
}

function drawBox(n) {
  const colour = colourFor(n.kind);
  const rx = 0;
  const body = n.shape === "db"
    ? drawDb(n, colour)
    : `<rect x="${n.x}" y="${n.y}" width="${n.w}" height="${n.h}" rx="${rx}" fill="${colour}" stroke="${colour}" stroke-width="2"/>`;
  const textX = n.x + n.w / 2;
  const textStart = n.y + Math.max(40, n.h * 0.30);
  const textColour = n.kind === "component" ? black : white;
  const typeColour = textColour;
  return `
    <g id="${esc(n.id)}">
      ${body}
      ${textBlock([n.name], textX, textStart, { size: n.titleSize ?? 17, weight: "700", color: textColour })}
      ${textBlock([n.type], textX, textStart + 22, { size: 10, color: typeColour, italic: true })}
      ${textBlock(wrap(n.description, n.wrap ?? 30), textX, textStart + 44, { size: 11, color: textColour, gap: 14 })}
    </g>`;
}

function drawBoundary(b) {
  const colour = externalGrey;
  return `
    <rect x="${b.x}" y="${b.y}" width="${b.w}" height="${b.h}" rx="0" fill="none" stroke="${colour}" stroke-width="2" stroke-dasharray="9 9"/>
    ${textBlock([b.name], b.x + 10, b.y + b.h - 34, { size: 13, weight: "700", color: black, align: "left" })}
    ${textBlock([b.type], b.x + 10, b.y + b.h - 16, { size: 10, color: black, align: "left", italic: true })}`;
}

function edgeParts(diagram, e) {
  const nodes = diagram.nodesById;
  const from = nodes[e.from];
  const to = nodes[e.to];
  const start = e.start ? e.start : anchor(from, e.fromSide ?? "right");
  const end = e.end ? e.end : anchor(to, e.toSide ?? "left");
  const points = [start, ...(e.via ?? []), end];
  const d = points.map((p, i) => `${i === 0 ? "M" : "L"} ${p.x} ${p.y}`).join(" ");
  const mid = e.labelAt ?? midpoint(points);
  const labelLines = wrap(e.label, e.labelWrap ?? 24);
  const tech = e.tech ? wrap(`[${e.tech}]`, e.techWrap ?? 24) : [];
  return {
    path: `<path d="${d}" fill="none" stroke="${relationGrey}" stroke-width="2" stroke-dasharray="10 10" marker-end="url(#arrow)"/>`,
    label: `<g>
      ${textBlock(labelLines, mid.x, mid.y, { size: e.labelSize ?? 11, color: black, gap: 14 })}
      ${textBlock(tech, mid.x, mid.y + labelLines.length * 14, { size: 10, color: black, italic: true, gap: 13 })}
    </g>`
  };
}

function midpoint(points) {
  const p = points[Math.floor((points.length - 1) / 2)];
  const q = points[Math.floor((points.length - 1) / 2) + 1] ?? p;
  return { x: (p.x + q.x) / 2, y: (p.y + q.y) / 2 };
}

function render(diagram) {
  diagram.nodesById = Object.fromEntries(diagram.nodes.map((n) => [n.id, n]));
  const boundaries = diagram.boundaries?.map(drawBoundary).join("\n") ?? "";
  const edgePartsList = diagram.edges.map((e) => edgeParts(diagram, e));
  const edgePaths = edgePartsList.map((e) => e.path).join("\n");
  const nodes = diagram.nodes.map((n) => n.kind === "person" ? drawPerson(n) : drawBox(n)).join("\n");
  const edgeLabels = edgePartsList.map((e) => e.label).join("\n");
  return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="${diagram.w}" height="${diagram.h}" viewBox="0 0 ${diagram.w} ${diagram.h}">
  ${marker()}
  <rect width="100%" height="100%" fill="white"/>
  ${boundaries}
  ${edgePaths}
  ${nodes}
  ${edgeLabels}
  ${textBlock([diagram.footer], 20, diagram.h - 52, { size: 19, weight: "700", color: black, align: "left" })}
  ${textBlock([diagram.footnote], 20, diagram.h - 30, { size: 11, color: relationGrey, align: "left" })}
</svg>`;
}

const diagrams = [
  {
    file: "01-context.svg",
    w: 1500,
    h: 980,
    title: "System Context diagram for OpenMRS Appointment Reminder Platform",
    footer: "System Context View: OpenMRS Appointment Reminder Platform",
    footnote: "Reviewed against both codebases. Notation follows the c4model.com boxes-and-lines examples.",
    nodes: [
      { id: "clinician", kind: "person", x: 90, y: 120, w: 260, h: 180, name: "Clinician", description: "Uses OpenMRS O3 to manage appointments." },
      { id: "admin", kind: "person", x: 90, y: 390, w: 260, h: 180, name: "System administrator", description: "Configures organizations, providers, templates, retries, and access." },
      { id: "patient", kind: "person", x: 90, y: 660, w: 260, h: 180, name: "Patient", description: "Receives appointment reminders by SMS or email." },
      { id: "platform", kind: "internal", x: 560, y: 365, w: 420, h: 150, name: "OpenMRS Appointment Reminder Platform", type: "[Software System]", description: "OpenMRS O3 plus communication backend for durable 24h and 1h appointment reminders.", wrap: 48 },
      { id: "providers", kind: "external-red", x: 1150, y: 270, w: 280, h: 140, name: "Messaging provider APIs", type: "[External Software System]", description: "SwiftSend, SecurePost, LegacyLink, and AsyncFlow compatible services.", wrap: 30 },
      { id: "monitoring", kind: "external-orange", x: 1150, y: 550, w: 280, h: 140, name: "Monitoring tools", type: "[External Software System]", description: "Prometheus and Grafana consume metrics and logs.", wrap: 30 }
    ],
    edges: [
      { from: "clinician", to: "platform", label: "Manages clinical appointment workflow", tech: "HTTPS", fromSide: "right", toSide: "left", labelAt: { x: 445, y: 220 } },
      { from: "admin", to: "platform", label: "Administers backend configuration and operations", tech: "HTTPS/JWT", fromSide: "right", toSide: "left", labelAt: { x: 435, y: 455 }, labelWrap: 28 },
      { from: "platform", to: "providers", label: "Sends reminder delivery requests", tech: "HTTPS JSON/XML", fromSide: "right", toSide: "left", labelAt: { x: 1060, y: 320 } },
      { from: "providers", to: "patient", label: "Delivers reminders", tech: "SMS/email", fromSide: "bottom", toSide: "right", via: [{ x: 1290, y: 810 }], labelAt: { x: 760, y: 810 } },
      { from: "monitoring", to: "platform", label: "Scrapes metrics and reads logs", tech: "HTTP /metrics, stdout", fromSide: "left", toSide: "right", labelAt: { x: 1060, y: 590 } }
    ]
  },
  {
    file: "02-containers.svg",
    w: 1780,
    h: 1180,
    title: "Container diagram for OpenMRS Appointment Reminder Platform",
    footer: "Container View: OpenMRS Appointment Reminder Platform",
    footnote: "C4 container means an application or data store, not necessarily a Docker container.",
    boundaries: [
      { x: 60, y: 190, w: 1420, h: 875, name: "OpenMRS Appointment Reminder Platform", type: "[Software System]" }
    ],
    nodes: [
      { id: "clinician", kind: "person", x: 80, y: 20, w: 220, h: 150, name: "Clinician", description: "Uses OpenMRS O3." },
      { id: "admin", kind: "person", x: 370, y: 20, w: 220, h: 150, name: "System administrator", description: "Operates the backend." },
      { id: "patient", kind: "person", x: 1530, y: 735, w: 220, h: 150, name: "Patient", description: "Receives reminders." },
      { id: "gateway", kind: "internal", x: 145, y: 245, w: 300, h: 145, name: "OpenMRS Gateway", type: "[Container: Nginx]", description: "Routes /openmrs browser and API traffic.", icon: "terminal" },
      { id: "spa", kind: "internal", x: 145, y: 470, w: 300, h: 145, name: "OpenMRS O3 Frontend", type: "[Container: JavaScript SPA]", description: "Clinical EMR user interface.", icon: "browser" },
      { id: "openmrs", kind: "internal", x: 545, y: 470, w: 320, h: 145, name: "OpenMRS Backend", type: "[Container: Java/Tomcat, OMOD]", description: "OpenMRS APIs and appointment webhook module.", icon: "terminal", wrap: 32 },
      { id: "openmrsdb", kind: "internal", shape: "db", x: 545, y: 730, w: 320, h: 150, name: "OpenMRS Database", type: "[Container: MariaDB]", description: "Clinical and OpenMRS configuration data.", wrap: 32 },
      { id: "api", kind: "internal", x: 990, y: 470, w: 360, h: 155, name: "Communication Backend", type: "[Container: ASP.NET Core .NET 10]", description: "REST API, webhook processing, workers, consumers, retention, and metrics.", icon: "terminal", wrap: 38 },
      { id: "pg", kind: "internal", shape: "db", x: 990, y: 730, w: 360, h: 150, name: "Communication Database", type: "[Container: PostgreSQL]", description: "Identity, configuration, reminders, retry ledger, templates, and audit logs.", wrap: 38 },
      { id: "rabbit", kind: "internal", x: 990, y: 245, w: 360, h: 145, name: "Reminder Message Broker", type: "[Container: RabbitMQ]", description: "Durable MassTransit command transport.", icon: "terminal", wrap: 36 },
      { id: "providers", kind: "external-red", x: 1530, y: 470, w: 230, h: 145, name: "Messaging provider APIs", type: "[External Software System]", description: "SwiftSend, SecurePost, LegacyLink, AsyncFlow.", wrap: 25 }
    ],
    edges: [
      { from: "clinician", to: "gateway", label: "Uses OpenMRS O3", tech: "HTTPS", fromSide: "bottom", toSide: "top", labelAt: { x: 255, y: 205 } },
      { from: "admin", to: "api", label: "Manages operations", tech: "HTTPS/JWT", fromSide: "bottom", toSide: "top", via: [{ x: 480, y: 210 }, { x: 1170, y: 210 }], labelAt: { x: 810, y: 205 } },
      { from: "gateway", to: "spa", label: "Serves SPA", tech: "HTTP", fromSide: "bottom", toSide: "top", labelAt: { x: 245, y: 430 } },
      { from: "gateway", to: "openmrs", label: "Routes OpenMRS APIs", tech: "HTTP", fromSide: "right", toSide: "top", labelAt: { x: 575, y: 330 } },
      { from: "spa", to: "gateway", label: "Calls APIs through gateway", tech: "HTTP", fromSide: "top", toSide: "bottom", labelAt: { x: 355, y: 430 } },
      { from: "openmrs", to: "openmrsdb", label: "Reads and writes clinical data", tech: "JDBC", fromSide: "bottom", toSide: "top", labelAt: { x: 710, y: 670 } },
      { from: "openmrs", to: "api", label: "Posts signed appointment events", tech: "HTTPS/JSON + HMAC", fromSide: "right", toSide: "left", labelAt: { x: 930, y: 485 } },
      { from: "api", to: "openmrs", label: "Reads patient and appointment data", tech: "FHIR R4 + REST", fromSide: "left", toSide: "right", labelAt: { x: 930, y: 585 } },
      { from: "api", to: "pg", label: "Persists backend state", tech: "SQL/TCP", fromSide: "bottom", toSide: "top", labelAt: { x: 1180, y: 680 } },
      { from: "api", to: "rabbit", label: "Publishes and consumes commands", tech: "AMQP", fromSide: "top", toSide: "bottom", labelAt: { x: 1170, y: 430 } },
      { from: "api", to: "providers", label: "Sends selected provider request", tech: "HTTPS JSON/XML", fromSide: "right", toSide: "left", labelAt: { x: 1440, y: 520 } },
      { from: "providers", to: "patient", label: "Delivers reminders", tech: "SMS/email", fromSide: "bottom", toSide: "top", labelAt: { x: 1645, y: 670 } }
    ]
  },
  {
    file: "03-backend-components.svg",
    w: 1880,
    h: 1280,
    title: "Component diagram for Communication Backend API and Workers",
    footer: "Component View: OpenMRS Appointment Reminder Platform - Communication Backend",
    footnote: "Components are logical groupings inside the ASP.NET Core container.",
    boundaries: [
      { x: 360, y: 130, w: 1050, h: 965, name: "Communication Backend API and Workers", type: "[Container: ASP.NET Core .NET 10]" }
    ],
    nodes: [
      { id: "admin", kind: "person", x: 40, y: 110, w: 230, h: 160, name: "System administrator", description: "Uses protected backend endpoints." },
      { id: "openmrs", kind: "internal", x: 40, y: 430, w: 260, h: 125, name: "OpenMRS Backend", type: "[Container: Java/Tomcat]", description: "Webhook source and FHIR/OpenMRS REST API.", icon: "terminal", wrap: 26 },
      { id: "providers", kind: "external-red", x: 1580, y: 780, w: 240, h: 125, name: "Messaging provider APIs", type: "[External Software System]", description: "SwiftSend, SecurePost, LegacyLink, AsyncFlow.", wrap: 25 },
      { id: "db", kind: "internal", shape: "db", x: 725, y: 1120, w: 300, h: 135, name: "Communication Database", type: "[Container: PostgreSQL]", description: "State, configuration, retry ledger, audit logs.", wrap: 32 },
      { id: "rabbit", kind: "container", x: 1580, y: 250, w: 240, h: 125, name: "RabbitMQ", type: "[Container: Message Broker]", description: "Transports SendReminderCommand.", wrap: 25 },
      { id: "controllers", kind: "internal", x: 430, y: 210, w: 245, h: 125, name: "API Controllers", type: "[Component: ASP.NET Core MVC]", description: "Auth, webhooks, reminders, messages, health.", icon: "component", wrap: 27 },
      { id: "auth", kind: "internal", x: 780, y: 210, w: 245, h: 125, name: "Authentication", type: "[Component: Identity + JWT]", description: "Admin bootstrap, login, roles, JWT policies.", icon: "component", wrap: 27 },
      { id: "validator", kind: "internal", x: 1110, y: 210, w: 245, h: 125, name: "Webhook Validator", type: "[Component: HMAC-SHA256]", description: "Checks organization, timestamp, and signature.", icon: "component", wrap: 27 },
      { id: "webhook", kind: "internal", x: 430, y: 455, w: 245, h: 125, name: "Webhook Processor", type: "[Component: C# service]", description: "Deduplicates events and schedules reminders.", icon: "component", wrap: 27 },
      { id: "org", kind: "internal", x: 780, y: 455, w: 245, h: 125, name: "Organization Config", type: "[Component: EF repository]", description: "Loads OpenMRS and provider settings per hospital.", icon: "component", wrap: 27 },
      { id: "openmrsClient", kind: "internal", x: 1110, y: 455, w: 245, h: 125, name: "OpenMRS Client", type: "[Component: HttpClient]", description: "Reads FHIR and OpenMRS REST data.", icon: "component", wrap: 27 },
      { id: "scheduler", kind: "internal", x: 430, y: 700, w: 245, h: 125, name: "Reminder Scheduler", type: "[Component: BackgroundService]", description: "Claims due reminders and publishes commands.", icon: "component", wrap: 27 },
      { id: "consumer", kind: "internal", x: 780, y: 700, w: 245, h: 125, name: "Reminder Consumer", type: "[Component: MassTransit]", description: "Consumes commands and records delivery results.", icon: "component", wrap: 27 },
      { id: "messaging", kind: "internal", x: 1110, y: 700, w: 245, h: 125, name: "Messaging Dispatch", type: "[Component: Provider adapters]", description: "Calls the configured provider only.", icon: "component", wrap: 27 },
      { id: "persistence", kind: "internal", x: 600, y: 930, w: 245, h: 125, name: "Persistence", type: "[Component: EF Core]", description: "Owns domain, identity, retry, and audit writes.", icon: "component", wrap: 27 },
      { id: "encryption", kind: "internal", x: 950, y: 930, w: 245, h: 125, name: "Encryption", type: "[Component: AES-256-GCM]", description: "Encrypts patient context and secrets at rest.", icon: "component", wrap: 27 }
    ],
    edges: [
      { from: "admin", to: "controllers", label: "Calls protected endpoints", tech: "HTTPS/JWT", fromSide: "right", toSide: "left", labelAt: { x: 345, y: 220 } },
      { from: "openmrs", to: "controllers", label: "Posts appointment webhook", tech: "HTTPS/JSON + HMAC", fromSide: "right", toSide: "left", labelAt: { x: 340, y: 455 } },
      { from: "controllers", to: "auth", label: "Authenticates users", fromSide: "right", toSide: "left", labelAt: { x: 730, y: 245 } },
      { from: "controllers", to: "validator", label: "Validates webhook", fromSide: "right", toSide: "left", via: [{ x: 730, y: 175 }, { x: 1080, y: 175 }], labelAt: { x: 930, y: 165 } },
      { from: "validator", to: "org", label: "Loads webhook secret", fromSide: "bottom", toSide: "top", labelAt: { x: 1110, y: 390 } },
      { from: "controllers", to: "webhook", label: "Submits accepted event", fromSide: "bottom", toSide: "top", labelAt: { x: 552, y: 390 } },
      { from: "webhook", to: "org", label: "Gets schedule policy", fromSide: "right", toSide: "left", labelAt: { x: 730, y: 500 } },
      { from: "webhook", to: "persistence", label: "Writes appointment and reminders", tech: "SQL", fromSide: "bottom", toSide: "top", labelAt: { x: 555, y: 840 } },
      { from: "scheduler", to: "persistence", label: "Claims due reminders", tech: "SQL", fromSide: "bottom", toSide: "top", labelAt: { x: 595, y: 875 } },
      { from: "scheduler", to: "rabbit", label: "Publishes command", tech: "AMQP", fromSide: "right", toSide: "left", via: [{ x: 1500, y: 760 }, { x: 1500, y: 310 }], labelAt: { x: 1480, y: 535 } },
      { from: "rabbit", to: "consumer", label: "Delivers command", tech: "AMQP", fromSide: "left", toSide: "right", via: [{ x: 1510, y: 760 }], labelAt: { x: 1270, y: 735 } },
      { from: "consumer", to: "openmrsClient", label: "Requests patient contact", fromSide: "top", toSide: "bottom", labelAt: { x: 1015, y: 630 } },
      { from: "openmrsClient", to: "openmrs", label: "Reads FHIR data", tech: "HTTPS", fromSide: "left", toSide: "right", via: [{ x: 360, y: 520 }], labelAt: { x: 620, y: 615 } },
      { from: "consumer", to: "messaging", label: "Sends rendered reminder", fromSide: "right", toSide: "left", labelAt: { x: 1075, y: 675 } },
      { from: "messaging", to: "providers", label: "Calls selected provider", tech: "HTTPS JSON/XML", fromSide: "right", toSide: "left", via: [{ x: 1505, y: 760 }, { x: 1505, y: 842 }], labelAt: { x: 1490, y: 820 } },
      { from: "consumer", to: "persistence", label: "Records result and retry state", tech: "SQL", fromSide: "bottom", toSide: "top", labelAt: { x: 810, y: 875 } },
      { from: "persistence", to: "encryption", label: "Encrypts sensitive fields", fromSide: "right", toSide: "left", labelAt: { x: 900, y: 965 } },
      { from: "persistence", to: "db", label: "Persists backend state", tech: "SQL/TCP", fromSide: "bottom", toSide: "top", labelAt: { x: 770, y: 1090 } }
    ]
  },
  {
    file: "04-openmrs-webhook-module-components.svg",
    w: 1600,
    h: 1080,
    title: "Component diagram for OpenMRS Appointment Webhook Module",
    footer: "Component View: OpenMRS Appointment Reminder Platform - OpenMRS Backend webhook module",
    footnote: "The custom OMOD is code inside the OpenMRS Backend container, not a separate C4 container.",
    boundaries: [
      { x: 370, y: 145, w: 860, h: 780, name: "OpenMRS Backend", type: "[Container: OpenMRS 2.8.6, Java/Tomcat]" }
    ],
    nodes: [
      { id: "api", kind: "internal", x: 1270, y: 430, w: 270, h: 130, name: "Communication Backend", type: "[Container: ASP.NET Core]", description: "Receives signed appointment webhooks.", icon: "terminal", wrap: 28 },
      { id: "db", kind: "internal", shape: "db", x: 60, y: 430, w: 260, h: 130, name: "OpenMRS Database", type: "[Container: MariaDB]", description: "Clinical data and global properties.", wrap: 28 },
      { id: "event", kind: "internal", x: 435, y: 230, w: 260, h: 120, name: "OpenMRS Event Module", type: "[Component: event-api]", description: "Publishes Encounter events.", icon: "component" },
      { id: "scheduler", kind: "internal", x: 865, y: 230, w: 260, h: 120, name: "OpenMRS Scheduler", type: "[Component: SchedulerService]", description: "Runs module retry task.", icon: "component" },
      { id: "listener", kind: "internal", x: 435, y: 455, w: 260, h: 120, name: "Appointment Event Listener", type: "[Component: Java EventListener]", description: "Extracts appointment event data.", icon: "component" },
      { id: "dispatcher", kind: "internal", x: 865, y: 455, w: 260, h: 120, name: "Webhook Dispatcher", type: "[Component: Java HTTP client]", description: "Posts signed webhook JSON.", icon: "component" },
      { id: "props", kind: "internal", x: 435, y: 680, w: 260, h: 120, name: "Webhook Properties", type: "[Component: Global properties]", description: "Resolves URL, org id, secret, outbox, retry interval.", icon: "component", wrap: 27 },
      { id: "outbox", kind: "internal", x: 865, y: 680, w: 260, h: 120, name: "File Webhook Outbox", type: "[Component: JSON Lines file]", description: "Stores failed dispatches.", icon: "folder" },
      { id: "signer", kind: "internal", x: 650, y: 835, w: 260, h: 120, name: "HMAC Signer", type: "[Component: HmacSHA256]", description: "Signs timestamp plus raw body.", icon: "component" },
      { id: "retry", kind: "internal", x: 1060, y: 835, w: 260, h: 120, name: "Webhook Retry Task", type: "[Component: Scheduler task]", description: "Replays failed outbox entries.", icon: "component" }
    ],
    edges: [
      { from: "event", to: "listener", label: "Publishes Encounter events", tech: "JMS MapMessage", fromSide: "bottom", toSide: "top", labelAt: { x: 565, y: 400 } },
      { from: "listener", to: "dispatcher", label: "Dispatches normalized payload", fromSide: "right", toSide: "left", labelAt: { x: 785, y: 500 } },
      { from: "dispatcher", to: "signer", label: "Requests signature", tech: "HMAC-SHA256", fromSide: "bottom", toSide: "top", labelAt: { x: 845, y: 765 } },
      { from: "dispatcher", to: "props", label: "Reads endpoint and secret", fromSide: "left", toSide: "right", labelAt: { x: 780, y: 650 } },
      { from: "props", to: "db", label: "Reads global properties", tech: "AdministrationService/JDBC", fromSide: "left", toSide: "right", labelAt: { x: 360, y: 640 } },
      { from: "dispatcher", to: "api", label: "Posts signed webhook", tech: "HTTPS/JSON + HMAC", fromSide: "right", toSide: "left", labelAt: { x: 1210, y: 470 } },
      { from: "dispatcher", to: "outbox", label: "Stores failed HTTP requests", fromSide: "bottom", toSide: "top", labelAt: { x: 1010, y: 640 } },
      { from: "scheduler", to: "retry", label: "Runs retry task", fromSide: "bottom", toSide: "top", labelAt: { x: 1180, y: 640 } },
      { from: "retry", to: "outbox", label: "Reads and rewrites entries", fromSide: "left", toSide: "bottom", labelAt: { x: 1030, y: 835 } },
      { from: "retry", to: "dispatcher", label: "Retries webhook send", fromSide: "top", toSide: "bottom", labelAt: { x: 1120, y: 650 } }
    ]
  },
  {
    file: "05-appointment-webhook-dynamic.svg",
    w: 1550,
    h: 950,
    title: "Dynamic diagram for signed appointment webhook scheduling",
    footer: "Dynamic View: Signed appointment webhook scheduling",
    footnote: "Numbered relationships show runtime order for the appointment change scenario.",
    boundaries: [
      { x: 345, y: 130, w: 800, h: 650, name: "OpenMRS Appointment Reminder Platform", type: "[Software System]" }
    ],
    nodes: [
      { id: "clinician", kind: "person", x: 60, y: 120, w: 230, h: 160, name: "Clinician", description: "Changes appointment." },
      { id: "openmrs", kind: "internal", x: 405, y: 190, w: 260, h: 125, name: "OpenMRS Backend", type: "[Container: Java/Tomcat]", description: "Publishes Encounter event.", icon: "terminal" },
      { id: "omod", kind: "internal", x: 805, y: 190, w: 260, h: 125, name: "Webhook Module", type: "[Component: OMOD]", description: "Builds and signs event JSON.", icon: "component" },
      { id: "api", kind: "internal", x: 805, y: 500, w: 260, h: 125, name: "Webhook Endpoint", type: "[Component: ASP.NET Core]", description: "Validates and accepts event.", icon: "component" },
      { id: "db", kind: "internal", shape: "db", x: 405, y: 500, w: 260, h: 135, name: "Communication Database", type: "[Container: PostgreSQL]", description: "Webhook log, appointment, scheduled reminders.", wrap: 28 },
      { id: "outbox", kind: "internal", x: 1190, y: 190, w: 260, h: 125, name: "Webhook Outbox", type: "[Component: JSONL file]", description: "Stores failed sends.", icon: "folder" }
    ],
    edges: [
      { from: "clinician", to: "openmrs", label: "1: Creates or updates appointment", tech: "OpenMRS O3", fromSide: "right", toSide: "left", labelAt: { x: 335, y: 185 }, labelWrap: 25 },
      { from: "openmrs", to: "omod", label: "2: Publishes Encounter event", tech: "event-api/JMS", fromSide: "right", toSide: "left", labelAt: { x: 735, y: 220 } },
      { from: "omod", to: "api", label: "3: Posts signed webhook", tech: "HTTPS/JSON + HMAC", fromSide: "bottom", toSide: "top", labelAt: { x: 935, y: 405 } },
      { from: "api", to: "db", label: "4: Reads organization secret", tech: "SQL/TCP", fromSide: "left", toSide: "right", labelAt: { x: 735, y: 500 } },
      { from: "api", to: "db", label: "5: Writes event, appointment, and reminders", tech: "SQL/TCP", fromSide: "bottom", toSide: "bottom", via: [{ x: 935, y: 690 }, { x: 535, y: 690 }], labelAt: { x: 735, y: 715 }, labelWrap: 30 },
      { from: "omod", to: "outbox", label: "Failure path: queues failed post", tech: "filesystem", fromSide: "right", toSide: "left", labelAt: { x: 1130, y: 245 }, labelWrap: 25 }
    ]
  },
  {
    file: "06-reminder-delivery-dynamic.svg",
    w: 1680,
    h: 980,
    title: "Dynamic diagram for due reminder delivery and retry",
    footer: "Dynamic View: Due reminder delivery and retry",
    footnote: "Provider fallback is intentionally absent; retries use the selected organization provider.",
    boundaries: [
      { x: 60, y: 140, w: 1250, h: 700, name: "OpenMRS Appointment Reminder Platform", type: "[Software System]" }
    ],
    nodes: [
      { id: "scheduler", kind: "internal", x: 130, y: 210, w: 250, h: 120, name: "Reminder Scheduler", type: "[Component: BackgroundService]", description: "Claims due reminders.", icon: "component" },
      { id: "db", kind: "internal", shape: "db", x: 505, y: 210, w: 270, h: 130, name: "Communication Database", type: "[Container: PostgreSQL]", description: "Schedule, retry ledger, audit logs.", wrap: 28 },
      { id: "rabbit", kind: "internal", x: 880, y: 210, w: 250, h: 120, name: "RabbitMQ", type: "[Container: Message Broker]", description: "Transports reminder command.", icon: "terminal" },
      { id: "consumer", kind: "internal", x: 880, y: 520, w: 250, h: 120, name: "Reminder Consumer", type: "[Component: MassTransit]", description: "Sends reminder and records result.", icon: "component" },
      { id: "openmrs", kind: "internal", x: 505, y: 520, w: 270, h: 120, name: "OpenMRS Backend", type: "[Container: FHIR R4 API]", description: "Returns current patient contact.", icon: "terminal" },
      { id: "provider", kind: "external-red", x: 1375, y: 420, w: 250, h: 120, name: "Messaging provider API", type: "[External Software System]", description: "Selected provider only.", wrap: 26 },
      { id: "patient", kind: "person", x: 1390, y: 650, w: 220, h: 160, name: "Patient", description: "Receives reminder." }
    ],
    edges: [
      { from: "scheduler", to: "db", label: "1: Claims due reminder", tech: "SQL/TCP", fromSide: "right", toSide: "left", labelAt: { x: 445, y: 245 } },
      { from: "scheduler", to: "rabbit", label: "2: Publishes SendReminderCommand", tech: "AMQP", fromSide: "right", toSide: "left", via: [{ x: 430, y: 160 }, { x: 840, y: 160 }], labelAt: { x: 650, y: 150 }, labelWrap: 28 },
      { from: "rabbit", to: "consumer", label: "3: Delivers command", tech: "AMQP", fromSide: "bottom", toSide: "top", labelAt: { x: 1005, y: 420 } },
      { from: "consumer", to: "openmrs", label: "4: Reads patient contact", tech: "HTTPS/FHIR", fromSide: "left", toSide: "right", labelAt: { x: 815, y: 550 } },
      { from: "consumer", to: "provider", label: "5: Sends selected provider request", tech: "HTTPS JSON/XML", fromSide: "right", toSide: "left", labelAt: { x: 1260, y: 500 }, labelWrap: 28 },
      { from: "provider", to: "patient", label: "6: Delivers reminder", tech: "SMS/email", fromSide: "bottom", toSide: "top", labelAt: { x: 1500, y: 610 } },
      { from: "consumer", to: "db", label: "7: Records result, retry, or dead-letter", tech: "SQL/TCP", fromSide: "left", toSide: "bottom", via: [{ x: 650, y: 740 }], labelAt: { x: 675, y: 700 }, labelWrap: 30 }
    ]
  },
  {
    file: "07-multi-hospital-landscape.svg",
    w: 1560,
    h: 980,
    title: "System Landscape diagram for multi-hospital OpenMRS integration",
    footer: "System Landscape View: Multiple OpenMRS deployments and one communication backend",
    footnote: "Each OpenMRS deployment has a separate organization id, webhook secret, OpenMRS credentials, provider configuration, and retry policy.",
    nodes: [
      { id: "admin", kind: "person", x: 60, y: 390, w: 230, h: 160, name: "System administrator", description: "Seeds and operates hospital configuration." },
      { id: "openmrsA", kind: "internal", x: 420, y: 180, w: 290, h: 135, name: "Hospital A OpenMRS O3", type: "[Software System]", description: "Own organization id, webhook secret, FHIR credentials, timezone, and provider settings.", wrap: 33 },
      { id: "openmrsB", kind: "internal", x: 420, y: 610, w: 290, h: 135, name: "Hospital B OpenMRS O3", type: "[Software System]", description: "Separate organization id, webhook secret, FHIR credentials, timezone, and provider settings.", wrap: 33 },
      { id: "backend", kind: "internal", x: 855, y: 390, w: 320, h: 150, name: "OpenMRS Communication Backend", type: "[Software System]", description: "Validates hospital-specific webhooks and sends reminders through configured providers.", wrap: 38 },
      { id: "providers", kind: "external-red", x: 1290, y: 390, w: 230, h: 130, name: "Messaging provider APIs", type: "[External Software System]", description: "SwiftSend, SecurePost, LegacyLink, AsyncFlow.", wrap: 25 },
      { id: "patientA", kind: "person", x: 1285, y: 120, w: 230, h: 160, name: "Hospital A patient", description: "Receives Hospital A reminders." },
      { id: "patientB", kind: "person", x: 1285, y: 650, w: 230, h: 160, name: "Hospital B patient", description: "Receives Hospital B reminders." }
    ],
    edges: [
      { from: "admin", to: "backend", label: "Seeds and manages organizations", tech: "JSON/env + HTTPS/JWT", fromSide: "right", toSide: "left", labelAt: { x: 575, y: 455 } },
      { from: "openmrsA", to: "backend", label: "Posts signed events for org A", tech: "HTTPS/JSON + HMAC", fromSide: "right", toSide: "left", labelAt: { x: 790, y: 270 } },
      { from: "backend", to: "openmrsA", label: "Reads patient data for org A", tech: "FHIR R4 + REST", fromSide: "left", toSide: "right", labelAt: { x: 790, y: 335 } },
      { from: "openmrsB", to: "backend", label: "Posts signed events for org B", tech: "HTTPS/JSON + HMAC", fromSide: "right", toSide: "left", labelAt: { x: 790, y: 650 } },
      { from: "backend", to: "openmrsB", label: "Reads patient data for org B", tech: "FHIR R4 + REST", fromSide: "left", toSide: "right", labelAt: { x: 790, y: 590 } },
      { from: "backend", to: "providers", label: "Sends via configured provider per organization", tech: "HTTPS JSON/XML", fromSide: "right", toSide: "left", labelAt: { x: 1235, y: 430 }, labelWrap: 31 },
      { from: "providers", to: "patientA", label: "Delivers org A reminder", tech: "SMS/email", fromSide: "top", toSide: "bottom", labelAt: { x: 1390, y: 325 } },
      { from: "providers", to: "patientB", label: "Delivers org B reminder", tech: "SMS/email", fromSide: "bottom", toSide: "top", labelAt: { x: 1390, y: 600 } }
    ]
  },
  {
    file: "08-local-deployment.svg",
    w: 1740,
    h: 1160,
    title: "Deployment diagram for local Docker development",
    footer: "Deployment View: Local Docker development",
    footnote: "Local URLs: OpenMRS O3 on localhost:3032, backend API on localhost:5111, FakeComWorld on localhost:1337.",
    boundaries: [
      { x: 40, y: 90, w: 1340, h: 960, name: "Developer machine", type: "[Deployment Node: Docker Desktop host]", kind: "deployment" },
      { x: 80, y: 150, w: 590, h: 735, name: "OpenMRS compose project", type: "[Deployment Node: 2.4-LU1-openMRS-Avans]", kind: "deployment" },
      { x: 740, y: 150, w: 590, h: 735, name: "Communication backend compose project", type: "[Deployment Node: OpenMRSmoduleBackend]", kind: "deployment" }
    ],
    nodes: [
      { id: "browser", kind: "internal", x: 1420, y: 160, w: 260, h: 130, name: "Browser", type: "[Container Instance: Web browser]", description: "OpenMRS O3 and Swagger.", icon: "browser", wrap: 25 },
      { id: "gateway", kind: "internal", x: 120, y: 230, w: 230, h: 115, name: "Gateway", type: "[Container Instance: Nginx]", description: "Publishes localhost:3032.", icon: "terminal", wrap: 24 },
      { id: "spa", kind: "internal", x: 390, y: 230, w: 230, h: 115, name: "O3 Frontend", type: "[Container Instance: SPA]", description: "Serves OpenMRS UI assets.", icon: "browser", wrap: 24 },
      { id: "openmrs", kind: "internal", x: 120, y: 455, w: 230, h: 115, name: "OpenMRS Backend", type: "[Container Instance: Java/Tomcat]", description: "Includes webhook OMOD.", icon: "terminal", wrap: 24 },
      { id: "mariadb", kind: "internal", shape: "db", x: 390, y: 455, w: 230, h: 120, name: "OpenMRS Database", type: "[Container Instance: MariaDB]", description: "OpenMRS data.", wrap: 24 },
      { id: "volume", kind: "internal", x: 255, y: 675, w: 230, h: 115, name: "OpenMRS data volume", type: "[Container: File system]", description: "OMOD outbox JSONL.", icon: "folder", wrap: 24 },
      { id: "api", kind: "internal", x: 780, y: 230, w: 260, h: 120, name: "Backend API and Workers", type: "[Container Instance: ASP.NET Core]", description: "Publishes localhost:5111.", icon: "terminal", wrap: 26 },
      { id: "pg", kind: "internal", shape: "db", x: 1040, y: 455, w: 230, h: 120, name: "PostgreSQL", type: "[Container Instance: Database]", description: "Backend state.", wrap: 24 },
      { id: "rabbit", kind: "internal", x: 780, y: 455, w: 230, h: 115, name: "RabbitMQ", type: "[Container Instance: Message broker]", description: "MassTransit transport.", icon: "terminal", wrap: 24 },
      { id: "fake", kind: "external-red", x: 1450, y: 580, w: 230, h: 120, name: "FakeComWorld", type: "[External Software System]", description: "Simulated messaging providers.", wrap: 24 }
    ],
    edges: [
      { from: "browser", to: "gateway", label: "Uses OpenMRS O3", tech: "HTTP localhost:3032", fromSide: "left", toSide: "right", labelAt: { x: 910, y: 185 } },
      { from: "browser", to: "api", label: "Uses Swagger", tech: "HTTP localhost:5111", fromSide: "left", toSide: "right", via: [{ x: 1400, y: 320 }, { x: 1080, y: 320 }], labelAt: { x: 1230, y: 330 } },
      { from: "gateway", to: "spa", label: "Serves SPA", tech: "HTTP", fromSide: "right", toSide: "left", labelAt: { x: 370, y: 260 } },
      { from: "gateway", to: "openmrs", label: "Routes OpenMRS APIs", tech: "HTTP", fromSide: "bottom", toSide: "top", labelAt: { x: 235, y: 400 } },
      { from: "openmrs", to: "mariadb", label: "Reads/writes", tech: "JDBC", fromSide: "right", toSide: "left", labelAt: { x: 370, y: 485 } },
      { from: "openmrs", to: "volume", label: "Writes outbox", tech: "filesystem", fromSide: "bottom", toSide: "top", labelAt: { x: 260, y: 620 } },
      { from: "openmrs", to: "api", label: "Posts signed webhook", tech: "host.docker.internal:5111", fromSide: "right", toSide: "left", via: [{ x: 700, y: 515 }], labelAt: { x: 665, y: 500 } },
      { from: "api", to: "gateway", label: "Reads OpenMRS data", tech: "host.docker.internal:3032", fromSide: "left", toSide: "right", via: [{ x: 690, y: 290 }], labelAt: { x: 650, y: 250 } },
      { from: "api", to: "pg", label: "Persists state", tech: "SQL/TCP", fromSide: "bottom", toSide: "top", labelAt: { x: 1040, y: 390 } },
      { from: "api", to: "rabbit", label: "Publishes and consumes commands", tech: "AMQP", fromSide: "bottom", toSide: "top", labelAt: { x: 900, y: 390 } },
      { from: "api", to: "fake", label: "Sends provider calls", tech: "HTTP localhost:1337", fromSide: "right", toSide: "left", labelAt: { x: 1325, y: 565 } }
    ]
  },
  {
    file: "09-production-deployment.svg",
    w: 1740,
    h: 1160,
    title: "Deployment diagram for production or staging",
    footer: "Deployment View: Production or staging",
    footnote: "Deployment nodes are intentionally technology-neutral where the repositories do not prescribe a specific orchestrator.",
    boundaries: [
      { x: 50, y: 120, w: 620, h: 750, name: "Hospital OpenMRS environment", type: "[Deployment Node: Container host or orchestrator]", kind: "deployment" },
      { x: 760, y: 120, w: 620, h: 750, name: "Communication backend environment", type: "[Deployment Node: Container host or orchestrator]", kind: "deployment" }
    ],
    nodes: [
      { id: "browser", kind: "internal", x: 80, y: 20, w: 230, h: 110, name: "Browser", type: "[Container Instance]", description: "Clinician/admin access.", icon: "browser", wrap: 24 },
      { id: "gateway", kind: "internal", x: 95, y: 210, w: 230, h: 110, name: "OpenMRS Gateway", type: "[Container Instance: Nginx]", description: "Routes O3 traffic.", icon: "terminal" },
      { id: "spa", kind: "internal", x: 390, y: 210, w: 230, h: 110, name: "O3 Frontend", type: "[Container Instance: SPA]", description: "Clinical UI.", icon: "browser" },
      { id: "openmrs", kind: "internal", x: 95, y: 430, w: 230, h: 110, name: "OpenMRS Backend", type: "[Container Instance: Java/Tomcat]", description: "OpenMRS APIs and webhook OMOD.", icon: "terminal", wrap: 24 },
      { id: "mariadb", kind: "internal", shape: "db", x: 390, y: 430, w: 230, h: 120, name: "OpenMRS Database", type: "[Container Instance: MariaDB]", description: "Clinical data.", wrap: 24 },
      { id: "volume", kind: "internal", x: 245, y: 650, w: 230, h: 110, name: "OpenMRS data volume", type: "[Infrastructure Node: File system]", description: "OpenMRS data and webhook outbox.", icon: "folder", wrap: 24 },
      { id: "proxy", kind: "internal", x: 805, y: 210, w: 230, h: 110, name: "TLS reverse proxy", type: "[Infrastructure Node]", description: "Terminates TLS and routes API traffic.", icon: "terminal", wrap: 24 },
      { id: "api", kind: "internal", x: 1095, y: 210, w: 240, h: 120, name: "Backend API and Workers", type: "[Container Instance: ASP.NET Core]", description: "Controllers, workers, consumers, retention, metrics.", icon: "terminal", wrap: 26 },
      { id: "pg", kind: "internal", shape: "db", x: 1095, y: 455, w: 240, h: 120, name: "PostgreSQL", type: "[Container Instance: Database]", description: "Configuration, reminders, retry ledger, audit.", wrap: 26 },
      { id: "rabbit", kind: "internal", x: 805, y: 455, w: 230, h: 110, name: "RabbitMQ", type: "[Container Instance: Message broker]", description: "Durable command transport.", icon: "terminal", wrap: 24 },
      { id: "monitoring", kind: "external-orange", x: 805, y: 660, w: 230, h: 110, name: "Monitoring stack", type: "[External Software System]", description: "Scrapes metrics and reads logs.", wrap: 24 },
      { id: "provider", kind: "external-red", x: 1460, y: 455, w: 230, h: 120, name: "Messaging provider APIs", type: "[External Software System]", description: "External SMS/email providers.", wrap: 24 },
      { id: "patient", kind: "person", x: 1460, y: 690, w: 220, h: 155, name: "Patient", description: "Receives reminder." }
    ],
    edges: [
      { from: "browser", to: "gateway", label: "Uses OpenMRS O3", tech: "HTTPS", fromSide: "bottom", toSide: "top", labelAt: { x: 185, y: 165 } },
      { from: "browser", to: "proxy", label: "Uses authorized operations", tech: "HTTPS/JWT", fromSide: "right", toSide: "top", via: [{ x: 920, y: 75 }], labelAt: { x: 570, y: 65 } },
      { from: "gateway", to: "spa", label: "Serves SPA", tech: "HTTP", fromSide: "right", toSide: "left", labelAt: { x: 360, y: 240 } },
      { from: "gateway", to: "openmrs", label: "Routes APIs", tech: "HTTP", fromSide: "bottom", toSide: "top", labelAt: { x: 210, y: 375 } },
      { from: "openmrs", to: "mariadb", label: "Reads/writes clinical data", tech: "JDBC", fromSide: "right", toSide: "left", labelAt: { x: 360, y: 475 } },
      { from: "openmrs", to: "volume", label: "Persists outbox", tech: "filesystem", fromSide: "bottom", toSide: "top", labelAt: { x: 250, y: 600 } },
      { from: "openmrs", to: "proxy", label: "Posts signed webhooks", tech: "HTTPS/JSON + HMAC", fromSide: "right", toSide: "left", via: [{ x: 715, y: 485 }, { x: 715, y: 265 }], labelAt: { x: 695, y: 380 } },
      { from: "proxy", to: "api", label: "Routes allowed traffic", tech: "HTTPS/private HTTP", fromSide: "right", toSide: "left", labelAt: { x: 1060, y: 245 } },
      { from: "api", to: "gateway", label: "Reads OpenMRS data", tech: "FHIR R4 + REST", fromSide: "left", toSide: "right", via: [{ x: 710, y: 270 }], labelAt: { x: 725, y: 215 } },
      { from: "api", to: "pg", label: "Persists backend state", tech: "SQL/TCP private", fromSide: "bottom", toSide: "top", labelAt: { x: 1215, y: 390 } },
      { from: "api", to: "rabbit", label: "Publishes and consumes commands", tech: "AMQP private", fromSide: "bottom", toSide: "top", labelAt: { x: 1050, y: 395 } },
      { from: "api", to: "provider", label: "Sends selected provider request", tech: "HTTPS JSON/XML", fromSide: "right", toSide: "left", labelAt: { x: 1415, y: 405 }, labelWrap: 28 },
      { from: "provider", to: "patient", label: "Delivers reminder", tech: "SMS/email", fromSide: "bottom", toSide: "top", labelAt: { x: 1575, y: 635 } },
      { from: "monitoring", to: "api", label: "Scrapes metrics", tech: "HTTP /metrics", fromSide: "top", toSide: "bottom", labelAt: { x: 1015, y: 615 } }
    ]
  }
];

await mkdir(outDir, { recursive: true });
for (const diagram of diagrams) {
  await writeFile(join(outDir, diagram.file), render(diagram), "utf8");
}
