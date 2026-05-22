# 7. Use Next.js 16.2.4 for SaaS frontend

Date: 2026-05-23

## Status

Accepted

## Context

We are building a SaaS application with a rich web frontend that needs to integrate with our ASP.NET Core backend. The frontend must handle user authentication, dashboard interactions, and form-based data entry. We evaluated multiple frameworks: Next.js, Blazor, Vue.js, and Angular.

## Decision

We chose **Next.js 16.2.4** (or later compatible version) as our frontend framework.

- Full-stack React with built-in API routes and server components.
- Deployed alongside or separate from the backend via Vercel or self-hosted.
- Integrates via standard HTTP/REST calls to our ASP.NET Core Web API.

**Rationale:**
- Large, mature community and extensive ecosystem (UI libs, AI tools, deployment platforms).
- Market-leading framework for SaaS applications; de facto standard for startups and SMBs.
- Rich tooling and broad availability of AI-assisted code generation and plugin systems.
- Considered alternatives:
  - **Blazor**: Market feedback indicates it's not a turnkey solution; steeper learning curve for typical web developers.
  - **Vue.js**: Small community; limited ecosystem and third-party tool support.
  - **Angular**: No team experience; heavier framework, overkill for our requirements.

## Consequences

- Team must develop TypeScript/React skills (or hire for them).
- Frontend deployments are independent of backend deployments; coordinate versioning.
- Next.js projects can become large; watch bundle size and implement code splitting.
- Strong alignment with modern web standards (ES modules, server/client boundaries).
- Easy to add new pages/APIs; lower onboarding friction for new contributors.