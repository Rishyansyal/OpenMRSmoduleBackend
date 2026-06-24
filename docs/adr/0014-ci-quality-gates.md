# 14. CI quality gates

Date: 2026-05-23
Status: Accepted, amended 2026-05-30

## Context

Shared repositories need repeatable checks for build regressions and leaked
secrets.

## Decision

- Backend CI restores, release-builds, runs xUnit tests with coverage, builds
  the Docker image, and runs Gitleaks.
- OpenMRS workflows build the distro and validate the webhook OMOD.
- Heavy full-stack acceptance remains a release or operator check.

## Consequences

- Pull requests fail early on common regressions.
- Docker runtime acceptance remains explicit because it requires a working
  engine and takes longer than unit-level checks.
