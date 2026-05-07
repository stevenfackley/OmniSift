# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

OmniSift is a .NET 10 Blazor + ASP.NET Core Web API with a Postgres + pgvector backend. Primary use case: vector-search over ingested content. Active development; **no deploy pipeline yet** — CI runs build/test/format on push and PRs.

## Repo layout

- `src/OmniSift.Api/` — ASP.NET Core Web API. Auth, ingest, vector search endpoints, OpenTelemetry observability.
- `src/OmniSift.Web/` — Blazor frontend (`App.razor`, `Layout/`, `Pages/`, `Services/`).
- `src/OmniSift.Shared/` — DTOs and contracts shared between API and Web.
- `tests/OmniSift.UnitTests/` — xUnit + Moq.
- `tests/OmniSift.IntegrationTests/` — EF Core InMemory + WebApplicationFactory.
- `docker-compose.yml` — local Postgres (pgvector/pgvector:pg16) for development.

## Stack details

- **.NET 10.0.103** (`global.json`, rollForward: latestFeature)
- **Database:** Postgres 16 + pgvector extension; EF Core 10 + Npgsql with snake_case naming convention
- **Auth + observability:** Serilog + OpenTelemetry (already wired)
- **CI:** consumes shared `stevenfackley/gh-actions/.github/workflows/ci-dotnet.yml@v1`
- **Lock files:** `RestorePackagesWithLockFile=true` via `Directory.Build.props`; lockfile commits expected on every Dependabot bump

## Commands

Run from repo root.

```bash
# Restore (matches CI)
dotnet restore --configfile NuGet.Config

# Build
dotnet build src/OmniSift.Api/OmniSift.Api.csproj -c Release --no-restore
dotnet build src/OmniSift.Web/OmniSift.Web.csproj -c Release --no-restore

# Test (all)
dotnet test -c Release

# Test (single — xUnit filter)
dotnet test --filter "FullyQualifiedName~SomeTestClass.SomeTest"

# Format check (matches CI's --verify-no-changes step)
dotnet format --verify-no-changes --no-restore

# Local Postgres for integration tests
docker compose up -d
```

## CI/CD mental model

```
push main / PR
  │
  ▼
ci  (uses stevenfackley/gh-actions/.../ci-dotnet.yml@v1, project-name: OmniSift)
  ├─ resolve-solution
  ├─ Build & Test                       (treats warnings as errors; runs dotnet format --verify-no-changes)
  ├─ Dependency & Telemetry Audit
  └─ Native AOT Publish                 (skipped — no executable project with OutputType=Exe)
```

No deploy pipeline. Deployment story is TBD.

## Conventions

- **TreatWarningsAsErrors = true** repo-wide. CA2007 / IDE0005 / format-whitespace classes have all bitten this repo at least once — check `.editorconfig` before suppressing.
- **CA2007 scoped off in `tests/**`** — xUnit1030 forbids `ConfigureAwait` in test methods, so the rule must be off there.
- **Commits:** Conventional Commits (`feat`, `fix`, `chore`, `docs`, `refactor`, `test`, `ci`).
- **Branches:** `main` is protected. Squash-merge via PR.
- **Lockfiles:** `packages.lock.json` commits go in the same commit as the dependency bump that triggered them.

## Don'ts

- Don't disable `TreatWarningsAsErrors` to dodge a real warning. Fix the warning or scope it via `.editorconfig` with a documented reason.
- Don't add static AWS keys or other cloud credentials. None should be needed in this repo today.
- Don't bump major dependencies without confirming AOT-incompatible changes (OmniSift may eventually want AOT).
