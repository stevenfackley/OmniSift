-- ============================================================
-- OmniSift Database Initialization Script
-- PostgreSQL 16 + pgvector
-- ============================================================
--
-- SCOPE — what this file owns:
--   Only what EF Core's migrations do NOT create:
--     1. The `omnisift_app` application role + its GRANTs.
--     2. Default privilege rules so future tables auto-grant.
--
-- WHAT RUNS FIRST (on a fresh container):
--   docker-entrypoint-initdb.d runs this file as the postgres
--   superuser before the .NET app starts. That is the only
--   moment a superuser is available to CREATE ROLE.
--
-- WHAT RUNS SECOND:
--   Program.cs calls Database.Migrate() at startup, which runs
--   EF's InitialCreate migration as the omnisift_app user.
--   InitialCreate owns: extensions, all tables, all indexes
--   (including the HNSW index), RLS policies, and the seed tenant.
--
-- See docs/schema-provisioning.md for the full analysis of why
-- the schema is split this way.
-- ============================================================

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

GRANT USAGE ON SCHEMA public TO omnisift_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO omnisift_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO omnisift_app;

-- Ensure tables created by future migrations also grant to omnisift_app.
-- This is the key reason init.sql must run BEFORE migrations: ALTER DEFAULT
-- PRIVILEGES is a superuser operation that seeds the permission template for
-- subsequent CREATE TABLE statements, including those run by EF at startup.
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO omnisift_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO omnisift_app;
