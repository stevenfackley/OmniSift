-- ============================================================
-- OmniSift Database Initialization Script
-- PostgreSQL 16 + pgvector
-- ============================================================
--
-- SCOPE — what this file owns:
--   Only what EF Core's migrations cannot do as a non-superuser:
--     1. Extensions (CREATE EXTENSION requires superuser in PG16).
--     2. The `omnisift_app` application role + its GRANTs.
--     3. Default privilege rules so future tables auto-grant.
--
-- WHAT RUNS FIRST (on a fresh container):
--   docker-entrypoint-initdb.d runs this file as the postgres
--   superuser before the .NET app starts. That is the only
--   moment a superuser is available to CREATE EXTENSION / CREATE ROLE.
--
-- WHAT RUNS SECOND:
--   Program.cs calls Database.Migrate() at startup, which runs
--   EF's InitialCreate migration as the omnisift_app user.
--   InitialCreate also contains `AlterDatabase().Annotation(...)` calls
--   that emit `CREATE EXTENSION IF NOT EXISTS` — those are safe because
--   Postgres short-circuits the IF NOT EXISTS check BEFORE the privilege
--   check, so the non-superuser migration succeeds without error.
--   InitialCreate owns: all tables, all indexes (including the HNSW
--   index), RLS policies, and the seed tenant.
--
-- See docs/schema-provisioning.md for the full analysis of why
-- the schema is split this way.
-- ============================================================

-- ============================================================
-- Extensions
-- Must run as superuser (postgres). EF's InitialCreate also emits
-- CREATE EXTENSION IF NOT EXISTS for both extensions, but when they
-- already exist Postgres short-circuits before the privilege check,
-- so the non-superuser migration succeeds without error.
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS vector;

-- ============================================================
-- Application Role
-- Create a non-superuser role for the application connection.
-- The superuser (postgres) bypasses RLS, so the app MUST
-- connect as this role for RLS to be enforced.
-- This block is idempotent (IF NOT EXISTS guard).
-- ============================================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'omnisift_app') THEN
        CREATE ROLE omnisift_app WITH LOGIN PASSWORD 'omnisift_dev';
    END IF;
END
$$;

-- USAGE   : required to reference schema-qualified names.
-- CREATE  : required for EF Migrate() to CREATE TABLE (including __EFMigrationsHistory).
-- In PG15+ the public schema no longer grants CREATE to PUBLIC by default;
-- omnisift_app needs it explicitly since it runs all EF migrations.
GRANT USAGE, CREATE ON SCHEMA public TO omnisift_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO omnisift_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO omnisift_app;

-- ALTER DEFAULT PRIVILEGES FOR ROLE omnisift_app seeds the privilege
-- template that Postgres applies whenever omnisift_app creates a new
-- object. Since omnisift_app runs EF's Migrate() and therefore owns the
-- tables it creates, it already has full rights on those tables by
-- virtue of ownership. These defaults are future-proofing stubs: if a
-- second role (e.g. a read-only reporting role) is added later, granting
-- it rights on existing tables + setting a matching default privilege here
-- ensures newly migrated tables are covered automatically — without
-- requiring another superuser script at that point.
--
-- NOTE: `FOR ROLE omnisift_app` requires the caller (postgres) to be
-- superuser OR a member of omnisift_app — postgres is superuser, so
-- this runs fine in docker-entrypoint-initdb.d.
ALTER DEFAULT PRIVILEGES FOR ROLE omnisift_app IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO omnisift_app;
ALTER DEFAULT PRIVILEGES FOR ROLE omnisift_app IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO omnisift_app;
