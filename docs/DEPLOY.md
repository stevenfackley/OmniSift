# OmniSift — Deploy Guide

Stack: ASP.NET Core Web API (`api`) + Blazor WASM served by nginx (`web`) + Postgres 16/pgvector (`db`).

---

## Routing overview

| Path | Serves |
|------|--------|
| `/` | Marketing site (`landing/`) — static HTML/CSS/JS |
| `/app/` | Blazor WebAssembly SPA (nginx rewrites to `index.html`) |
| `http://api:8080` | ASP.NET Core API (internal; exposed on host port 5000 in dev) |

The nginx config (`src/OmniSift.Web/nginx.conf`) routes `/app/` to `/usr/share/nginx/html/app/` and falls back to `index.html` for SPA deep-links. `landing/` is copied to the nginx root so `/` serves the marketing site.

---

## GHCR image names

The deploy workflow pushes to GitHub Container Registry under the repo owner's namespace:

| Service | Image |
|---------|-------|
| Web (nginx + Blazor WASM) | `ghcr.io/stevenfackley/omnisift-web` |
| API (ASP.NET Core) | `ghcr.io/stevenfackley/omnisift-api` |

Each push to `main` (or manual `workflow_dispatch`) tags images with:
- `latest` — always the most recent build
- `prod-<7-char-SHA>` — immutable, e.g. `prod-3810ff9`

---

## CI/CD workflow

`.github/workflows/deploy.yml` runs on `push` to `main` and on `workflow_dispatch`.

### Job 1 — `build-push` (always runs; no live host needed)

1. Logs in to GHCR with the built-in `GITHUB_TOKEN` (`packages: write` permission).
2. Builds `src/OmniSift.Web/Dockerfile` (build context: repo root) → pushes as `omnisift-web`.
3. Builds `src/OmniSift.Api/Dockerfile` (build context: repo root) → pushes as `omnisift-api`.
4. Uses GitHub Actions cache (`type=gha`) per image to speed up layer reuse.

### Job 2 — `deploy` (skipped until secrets are set)

Runs only when `DEPLOY_HOST` secret is non-empty. SSHes to the production host and runs:

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml pull web api
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --no-deps --remove-orphans web api
```

---

## Required GitHub secrets

Set these in **Settings → Secrets and variables → Actions** on the GitHub repo.

### Always required (GHCR push)

None beyond the built-in `GITHUB_TOKEN` — it has `packages: write` automatically.

### Required for the SSH deploy step

| Secret | Description |
|--------|-------------|
| `DEPLOY_HOST` | SSH hostname or IP of the production server |
| `DEPLOY_USER` | SSH username (must be in the `docker` group) |
| `DEPLOY_SSH_KEY` | PEM-encoded private SSH key (passphrase-free) |
| `DEPLOY_WORK_DIR` | Absolute path on host where compose files live, e.g. `/srv/omnisift` |

Until these are set, the `deploy` job is skipped; `build-push` still runs and images are pushed to GHCR.

---

## Prerequisites on the production host

Before the first SSH deploy, the following must exist on the server at `$DEPLOY_WORK_DIR`:

```
docker-compose.yml         # copied from repo
docker-compose.prod.yml    # copied from repo
.env.prod                  # non-secret overrides; do NOT commit real values
secrets/
  postgres_password.txt
  Anthropic__ApiKey.txt
  OpenAI__ApiKey.txt
  Tavily__ApiKey.txt
  Jwt__Secret.txt
  ConnectionStrings__DefaultConnection.txt
```

### Populating secrets on first deploy

```bash
# Example — source from environment or your secrets manager
echo -n "$PG_PASSWORD"         > secrets/postgres_password.txt
echo -n "$ANTHROPIC_KEY"       > secrets/Anthropic__ApiKey.txt
echo -n "$OPENAI_KEY"          > secrets/OpenAI__ApiKey.txt
echo -n "$TAVILY_KEY"          > secrets/Tavily__ApiKey.txt
echo -n "$(openssl rand -hex 32)" > secrets/Jwt__Secret.txt
echo -n "Host=db;Port=5432;Database=omnisift;Username=omnisift_app;Password=$PG_PASSWORD" \
                                > secrets/ConnectionStrings__DefaultConnection.txt
chmod 600 secrets/*.txt
```

`secrets/*.txt` is gitignored. In automated pipelines, write these files from Vault, AWS Secrets Manager, Azure Key Vault, etc., at deploy time before running compose.

---

## Secret files

Docker file-based secrets keep sensitive values out of env vars and image layers. The API reads `/run/secrets/` via `AddKeyPerFile` in `Program.cs`. The Postgres image reads `POSTGRES_PASSWORD_FILE` natively.

---

## Production deploy — manual steps

```bash
# 1. Pull latest images
docker compose -f docker-compose.yml -f docker-compose.prod.yml pull web api

# 2. Restart web and api (leave db running)
docker compose -f docker-compose.yml -f docker-compose.prod.yml \
  up -d --no-deps --remove-orphans web api

# 3. Check health
docker compose ps
docker compose logs api --tail 50
```

First-time full start (including db):

```bash
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

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

### `.env.prod` (gitignored — no real secrets; keys come from Docker secrets)

Override `ASPNETCORE_ENVIRONMENT=Production` and any non-secret parameters. Sensitive values are injected via Docker file-based secrets, not env vars.

### Overriding the image tag at deploy time

The prod compose file reads `IMAGE_TAG` and `IMAGE_WEB` / `IMAGE_API` from the environment, defaulting to `latest`. To pin a specific SHA:

```bash
IMAGE_TAG=prod-3810ff9 docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

---

## Ports

| Service | Internal | Host (dev) | Prod |
|---------|----------|------------|------|
| `db` (Postgres) | 5432 | 5432 | internal only |
| `api` (ASP.NET) | 8080 | 5000 | internal only |
| `web` (nginx) | 80 | 5080 | 80/443 via reverse proxy |

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

## Hosted demo — pending infra

A live demo at a public URL is planned but deferred pending infrastructure provisioning (server, domain, TLS, secrets management). Once infra is ready:

1. Set the four `DEPLOY_*` GitHub secrets listed above.
2. Push to `main` (or trigger `workflow_dispatch`) — the deploy job will go live automatically.

See `docs/marketability-roadmap.md` item 10 for tracking.
