-- ============================================================
-- OmniSift Database Initialization Script
-- PostgreSQL 16 + pgvector
-- Multi-tenant schema with Row-Level Security (RLS)
-- ============================================================

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "vector";

-- ============================================================
-- Table: tenants
-- Root table for multi-tenancy. Not RLS-protected itself
-- (admin operations need cross-tenant access).
-- ============================================================
CREATE TABLE tenants (
    id          UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name        VARCHAR(256) NOT NULL,
    slug        VARCHAR(128) NOT NULL UNIQUE,
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_tenants_slug ON tenants (slug);

-- ============================================================
-- Table: data_sources
-- Tracks every uploaded/ingested data source per tenant.
-- ============================================================
CREATE TABLE data_sources (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    source_type     VARCHAR(50) NOT NULL CHECK (source_type IN ('pdf', 'sms', 'web')),
    file_name       VARCHAR(512),
    original_url    VARCHAR(2048),
    status          VARCHAR(50) NOT NULL DEFAULT 'pending'
                        CHECK (status IN ('pending', 'processing', 'completed', 'failed')),
    error_message   TEXT,
    metadata        JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_data_sources_tenant ON data_sources (tenant_id);
CREATE INDEX idx_data_sources_status ON data_sources (tenant_id, status);

-- ============================================================
-- Table: document_chunks
-- Individual text chunks with vector embeddings for semantic
-- search. Uses text-embedding-3-large (3072 dimensions).
-- ============================================================
CREATE TABLE document_chunks (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    data_source_id  UUID NOT NULL REFERENCES data_sources(id) ON DELETE CASCADE,
    content         TEXT NOT NULL,
    chunk_index     INTEGER NOT NULL,
    token_count     INTEGER NOT NULL DEFAULT 0,
    embedding       VECTOR(3072),
    metadata        JSONB NOT NULL DEFAULT '{}',
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_document_chunks_tenant ON document_chunks (tenant_id);
CREATE INDEX idx_document_chunks_source ON document_chunks (data_source_id);

-- HNSW index for fast approximate nearest neighbor search
-- Using cosine distance (most common for text embeddings)
CREATE INDEX idx_document_chunks_embedding ON document_chunks
    USING hnsw (embedding vector_cosine_ops)
    WITH (m = 16, ef_construction = 64);

-- ============================================================
-- Table: query_history
-- Stores agent query/response pairs for audit and context.
-- ============================================================
CREATE TABLE query_history (
    id              UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    tenant_id       UUID NOT NULL REFERENCES tenants(id) ON DELETE CASCADE,
    query_text      TEXT NOT NULL,
    response_text   TEXT,
    plugins_used    JSONB NOT NULL DEFAULT '[]',
    sources         JSONB NOT NULL DEFAULT '[]',
    duration_ms     INTEGER,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_query_history_tenant ON query_history (tenant_id);
CREATE INDEX idx_query_history_created ON query_history (tenant_id, created_at DESC);

-- ============================================================
-- Row-Level Security (RLS) Policies
-- Every tenant-scoped table enforces isolation via the
-- session variable 'app.current_tenant'.
-- ============================================================

-- Helper: Set a default for the session variable so RLS
-- doesn't fail if unset (returns empty string → no rows match)
-- The API middleware sets this on every request.

-- data_sources RLS
ALTER TABLE data_sources ENABLE ROW LEVEL SECURITY;
ALTER TABLE data_sources FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_data_sources ON data_sources
    FOR ALL
    USING (tenant_id = current_setting('app.current_tenant', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.current_tenant', true)::uuid);

-- document_chunks RLS
ALTER TABLE document_chunks ENABLE ROW LEVEL SECURITY;
ALTER TABLE document_chunks FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_document_chunks ON document_chunks
    FOR ALL
    USING (tenant_id = current_setting('app.current_tenant', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.current_tenant', true)::uuid);

-- query_history RLS
ALTER TABLE query_history ENABLE ROW LEVEL SECURITY;
ALTER TABLE query_history FORCE ROW LEVEL SECURITY;

CREATE POLICY tenant_isolation_query_history ON query_history
    FOR ALL
    USING (tenant_id = current_setting('app.current_tenant', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.current_tenant', true)::uuid);

-- ============================================================
-- Application Role
-- Create a non-superuser role for the application connection.
-- The superuser (postgres) bypasses RLS, so the app must
-- connect as this role for RLS to be enforced.
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

-- Ensure future tables also grant to omnisift_app
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO omnisift_app;
ALTER DEFAULT PRIVILEGES IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO omnisift_app;

-- ============================================================
-- Seed: Default Development Tenant
-- ============================================================
INSERT INTO tenants (id, name, slug)
VALUES ('a1b2c3d4-e5f6-7890-abcd-ef1234567890', 'Development Tenant', 'dev')
ON CONFLICT (slug) DO NOTHING;
