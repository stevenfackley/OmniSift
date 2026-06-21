# OmniSift — Deploy Guide

Stack: ASP.NET Core Web API (`api`) + Blazor WASM served by nginx (`web`) + Postgres 16/pgvector (`db`).

---

## Prerequisites

- Docker Engine 24+ and Docker Compose v2 (`docker compose`)
- `./secrets/*.txt` files populated (see below)
- `.env.prod` present at repo root (non-secret overrides; committed skeleton is `.env.example`)

---

## Routing overview

| Path | Serves |
|------|--------|
| `/` | Marketing site (`landing/`) — static HTML/CSS/JS |
| `/app/` | Blazor WebAssembly SPA (nginx rewrites to `index.html`) |
| `http://api:8080` | ASP.NET Core API (internal; exposed on host port 5000 in dev) |

The nginx config (`src/OmniSift.Web/nginx.conf`) routes `/app/` requests to `/usr/share/nginx/html/app/` and falls back to `index.html` for SPA deep-links. `landing/` is copied to the nginx root so `/` serves the marketing site.

---

## Secret files

Docker file-based secrets keep sensitive values out of env vars and image layers. Each file must contain only the raw secret value (no trailing newline required).

```
secrets/
  postgres_password.txt
  Anthropic__ApiKey.txt
  OpenAI__ApiKey.txt
  Tavily__ApiKey.txt
  Jwt__Secret.txt
  ConnectionStrings__DefaultConnection.txt
```

The API reads `/run/secrets/` via `AddKeyPerFile` in `Program.cs`. The Postgres image reads `POSTGRES_PASSWORD_FILE` natively.

`secrets/*.txt` is gitignored. In CI/CD, write these files from your secrets manager (Vault, AWS Secrets Manager, etc.) at deploy time.

---

## Production deploy

```bash
# 1. Populate secrets
echo -n "$(openssl rand -hex 32)" > secrets/Jwt__Secret.txt
echo -n "your-pg-password"        > secrets/postgres_password.txt
# ... fill remaining *.txt files

# 2. Build images (or pull from registry)
docker compose -f docker-compose.yml -f docker-compose.prod.yml build

# 3. Start stack
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d

# 4. Check health
docker compose ps
docker compose logs api --tail 50
```

The prod overlay (`docker-compose.prod.yml`) does the following on top of the base:
- Switches `env_file` to `.env.prod` for both `api` and `db`
- Mounts Docker secrets into the `api` and `db` containers
- Clears the dev env-var mappings for secrets (so the API reads only from `/run/secrets/`)

---

## Environment variables

### `.env.dev` (safe to commit — no real secrets)

| Variable | Purpose |
|----------|---------|
| `POSTGRES_USER` / `POSTGRES_PASSWORD` / `POSTGRES_DB` | Local Postgres credentials |
| `ANTHROPIC_API_KEY` / `OPENAI_API_KEY` / `TAVILY_API_KEY` | LLM + search API keys |
| `JWT_SECRET` / `JWT_ISSUER` / `JWT_AUDIENCE` | Auth token settings |
| `ASPNETCORE_ENVIRONMENT` | `Development` in dev |
| `API_BASE_URL` | Blazor's API endpoint (dev only) |

### `.env.prod` (gitignored — no real secrets either; keys come from Docker secrets)

Override `ASPNETCORE_ENVIRONMENT=Production` and any non-secret connection parameters. Sensitive values are injected via Docker file-based secrets, not env vars.

---

## Ports

| Service | Internal | Host (dev) |
|---------|----------|------------|
| `db` (Postgres) | 5432 | 5432 |
| `api` (ASP.NET) | 8080 | 5000 |
| `web` (nginx) | 80 | 5080 |

In production, place a reverse proxy (nginx, Caddy, Cloudflare Tunnel) in front of port 80/443 and do not expose `db` or `api` directly.

---

## Local dev (docker compose only)

```bash
cp .env.example .env.dev   # first time only — fill in your dev keys
docker compose up -d
# API: http://localhost:5000
# Web: http://localhost:5080
# App: http://localhost:5080/app/
```

---

## Rebuilding after code changes

```bash
docker compose build web api
docker compose up -d --no-deps web api
```

---

## Database migrations

EF Core migrations are applied via the API on startup (or run manually):

```bash
docker compose exec api dotnet OmniSift.Api.dll --migrate
# or from host:
dotnet ef database update --project src/OmniSift.Api
```

---

## No deploy pipeline yet

CI (`.github/workflows/`) runs build/test/format only. A deploy pipeline is on the roadmap (see `docs/marketability-roadmap.md` item 10).
