# Schema Provisioning Analysis

**Status:** implemented (2026-06-18)

## The conflict

Two mechanisms both provision the OmniSift schema on a fresh database:

| Mechanism | Runs as | When | Owns |
|-----------|---------|------|------|
| `infra/db/init.sql` | `postgres` superuser | `docker-entrypoint-initdb.d` at container first-boot | *Was:* everything. *Now:* role + GRANTs only. |
| EF `InitialCreate` migration | `omnisift_app` (non-superuser) | `Program.cs` → `Database.Migrate()` at startup | Tables, indexes, HNSW, RLS policies, seed tenant. |

On a fresh `docker compose up` both ran in sequence, creating duplicate tables, indexes, and RLS policies. Postgres `CREATE TABLE` is not idempotent — the second run threw errors that were swallowed inside `InitialCreate` only because Migrate() detects the migration is already applied (it checks `__EFMigrationsHistory`). But this was fragile: if the table creation order or the history table raced, errors surfaced.

## Decision: EF is the single source of truth

EF wins because:
- Its migrations are versioned, diffable, and reversible.
- `InitialCreate` already owns the full schema definition in C# types; keeping a parallel SQL copy was duplication waiting to drift.
- EF's snapshot prevents accidental column re-ordering or type mismatches.

## What init.sql keeps (and why it must)

`omnisift_app` is a non-superuser login role. Creating it requires `postgres` (superuser), which is only available during `docker-entrypoint-initdb.d`. EF runs as `omnisift_app` — it cannot create its own role, and it cannot GRANT on tables that don't exist yet. So the split is:

1. `init.sql` (superuser, runs first):
   - Creates `omnisift_app` role (idempotent DO $$ block).
   - GRANTs current + future table/sequence privileges via `ALTER DEFAULT PRIVILEGES`.

2. EF `InitialCreate` (omnisift_app, runs second):
   - Extensions (`uuid-ossp`, `vector`).
   - All tables, indexes (including HNSW), RLS policies.
   - Seed tenant row.

## What was removed from init.sql

- `CREATE EXTENSION IF NOT EXISTS` blocks — EF handles these.
- `CREATE TABLE` blocks for all four tables — EF handles these.
- All `CREATE INDEX` statements — EF handles these (including the HNSW raw-SQL block).
- All `ALTER TABLE … ENABLE ROW LEVEL SECURITY` and `CREATE POLICY` blocks — EF handles these.
- `INSERT INTO tenants` seed row — EF's `InitialCreate.InsertData()` handles this.

## Schema differences between old init.sql and EF

The `tenants` table in the old `init.sql` was missing the `api_key_hash VARCHAR(64)` column that EF added. This means any environment relying on init.sql alone was missing that column — RLS-guarded operations that needed the hash would fail silently. EF is now the authoritative definition.

## Verification path

### Docker path (init.sql + Migrate())
```bash
docker compose down -v      # wipe volume
docker compose up -d db     # init.sql runs
# wait for healthy
docker compose up -d api    # Migrate() runs InitialCreate
docker compose logs api | grep "Applied migration"
```
Expected: migration applied once; no duplicate-object errors.

### EF-only path (no init.sql, e.g. CI against a bare pgvector container)
```bash
# Start a bare pgvector container with a superuser-only connection string
docker run -d --name pg-test \
  -e POSTGRES_PASSWORD=test \
  -p 5433:5432 \
  pgvector/pgvector:pg16

# The omnisift_app role won't exist, so connect as postgres.
# Set connection string to use postgres user for the migrate step:
dotnet ef database update \
  --project src/OmniSift.Api \
  --connection "Host=localhost;Port=5433;Database=omnisift;Username=postgres;Password=test"
```
This proves EF can stand alone without init.sql. The tradeoff: in production you'd still need init.sql (or a one-time superuser script) to create `omnisift_app` before the app starts.

## TODO: production hardening

- `omnisift_dev` is the password in init.sql. Production deployments should override `POSTGRES_PASSWORD` and pass the real credentials via docker secrets — the app already reads `ConnectionStrings__DefaultConnection` from the environment, so the role password just needs to match.
- Consider extracting the role-creation block into a separate `infra/db/create-role.sql` script that can be run by a DBA without re-running the full init sequence.
