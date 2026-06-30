# Decisions

ADR log. One entry per architectural decision. Append-only; supersede with a new entry.

## Format

```
## {{DATE}} — {{title}}
**Status:** proposed | accepted | superseded by #N
**Context:** why we had to decide
**Decision:** what we chose
**Consequences:** what follows (pros, cons, risks)
```

---

## {{DATE}} — Initial stack: .NET 10 Native AOT

**Status:** accepted
**Context:** Greenfield service under portfolio `repo-template-dotnet10-aot`. Target: fast cold-start, small image, Linux deploys.
**Decision:** .NET 10 with `PublishAot=true`, `linux-musl-x64`, distroless static runtime.
**Consequences:**
- Cold start < 100ms, image ~15MB.
- Reflection, dynamic code gen restricted — must stay AOT-compatible.
- No Application Insights SDK (banned by CI); stdout logs only.

---

## 2026-06-30 — Supabase ES256 auth + tenant derived from the JWT claim

**Status:** accepted
**Context:** Tenant identity was taken from the client-supplied `X-Tenant-Id` header (and an `X-API-Key` middleware). The header won over the JWT claim, so any authenticated caller could read another tenant's data by changing the header — a tenant-spoofing hole. The portfolio standard (square-log / SquareLog) is Supabase-issued ES256 JWTs validated via OIDC discovery, no symmetric secret.
**Decision:**
- Validate Supabase ES256/RS256 access tokens via OIDC discovery (`MetadataAddress = {Supabase:Url}/auth/v1/.well-known/openid-configuration`, audience `authenticated`, `MapInboundClaims=false`). Enabled by setting `Supabase:Url`. No symmetric secret in this path.
- Tenant is derived ONLY from the validated `tenant_id` claim (`ClaimsPrincipalExtensions.TryGetTenantId`). The `X-Tenant-Id` header is no longer read for authorization. The Supabase project must emit `tenant_id` as a custom claim (app_metadata / access-token hook).
- Data controllers are `[Authorize]`; health + `/api/auth` stay anonymous. The `X-API-Key`/`X-Tenant-Id` middleware (including the global-key impersonation path) was removed.
- The in-house HS256 IdP (`AuthController` register/login + `JwtTokenService`) is retained as a **development-only fallback** used when `Supabase:Url` is empty; integration tests run on it. It is slated for removal once the Blazor frontend migrates to Supabase auth.
**Consequences:**
- Closes the tenant-spoofing hole; spoofing is covered by integration tests.
- Production requires a Supabase project + a `tenant_id` custom claim. The Blazor frontend still logs in against the in-house IdP — its migration to Supabase is a follow-up (it already sends Bearer; the stale `X-Tenant-Id` header it adds is now ignored).

---

## 2026-06-30 — Defer at-rest encryption of PII columns

**Status:** accepted (deferred)
**Context:** The SaaS stores personal docs / SMS bodies. The primary PII column is `document_chunks.content`. Transparent EF value-converter encryption was considered for this PR.
**Decision:** Defer column encryption. `document_chunks.content` is backed by a GIN `pg_trgm` index (`ix_document_chunks_content_trgm`) that powers the hybrid keyword-search arm (`VectorSearchPlugin` `word_similarity`/ILIKE). Encrypting the column at rest makes the ciphertext unsearchable and silently breaks keyword search. A correct solution (searchable/deterministic encryption, tokenization, or dropping the keyword arm in favour of the vector arm) is a design decision of its own and is out of scope for the auth PR.
**Consequences:**
- Auth + tenant-isolation ship now; PII-at-rest encryption tracked as a follow-up.
- Interim mitigations already present: tenant RLS, PII detection flags (`PiiScanner`), and TLS in transit.
