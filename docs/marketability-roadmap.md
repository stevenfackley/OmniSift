# OmniSift — Marketability Roadmap

Ten upgrades that move OmniSift from internal prototype to shippable product.

| # | Feature | Description | Status |
|---|---------|-------------|--------|
| 1 | Structured citations that render | Every answer chunk links back to its source document/URL with footnotes visible in the UI, not raw JSON | **Done** |
| 2 | One-click exportable cited report | Download a formatted cited report (Markdown via `/api/agent/report`, PDF via `/api/agent/report/pdf`); shareable outside the app | **Done** |
| 3 | Segment-specific onboarding + sample data | Role picker (genealogist / journalist / legal / investigator) + one-click "Load sample corpus" so users see value fast | **Done** |
| 4 | Auth + self-serve signup + multi-user | Register/login pages, JWT accepted alongside the dev header flow, per-tenant RLS, users table | In progress (signup + JWT + RLS done; **Stripe billing & email verification deferred**) |
| 5 | Streaming + markdown rendering | Tokens stream from the LLM to the Blazor UI (SSE); answers render markdown, not raw text | **Done** |
| 6 | Hybrid search + reranking + confidence | Vector + `pg_trgm` keyword fused via Reciprocal Rank Fusion + relevance threshold; per-source scores surfaced | **Done** |
| 7 | Trust/compliance: audit log + PII flagging + GDPR | Per-tenant audit log; PII detection wired into ingestion + UI badge; right-to-erasure export/delete | **Done** |
| 8 | Broaden ingestion: DOCX/email/OCR + async pipeline | DOCX + email (`.eml`/`.mbox`) extractors; RLS-safe async background queue; **OCR deferred** | In progress (DOCX/email/async done; **OCR deferred** — native deps) |
| 9 | Entity timelines & relationship graphs | On-demand NER over the corpus → interactive relationship graph (vis-network) + timeline; no graph datastore | **Done** |
| 10 | Pricing/packaging + hosted demo + deploy story | Public pricing tiers, GHCR build/push workflow, production Docker Compose | In progress (pricing + CI deploy + prod compose done; **hosted demo deferred** — needs infra/creds) |

---

## Legend

- **Done** — shipped and wired end-to-end
- **In progress** — core shipped; the noted gap remains
- **Deferred** — backlog; not blocked, just not prioritised yet

---

## Remaining gaps (deliberately deferred)

- **Item 4 (auth):** Stripe billing hooks and email verification. JWT issuance, the dual-auth (JWT *or* header) path, register/login UI, and RLS are done. Enforcing `[Authorize]` on existing endpoints is a non-breaking follow-up (the API accepts JWT today; the Web client sends it after login).
- **Item 8 (ingestion):** OCR for scanned PDFs (Tesseract / AWS Textract) — needs native binaries; deferred to keep the build/runtime clean.
- **Item 10 (hosted demo):** A live demo URL needs a server, domain, TLS, and the four `DEPLOY_*` GitHub secrets. The deploy workflow's `build-push` job runs today and accumulates GHCR images; the `deploy` SSH job no-ops cleanly until the secrets exist.
- **Item 9 (entity graph):** Graphs are recomputed on demand (a chat-completion over up to ~16K chars of corpus per "Build graph"). If usage grows, cache per-tenant keyed on a corpus hash rather than re-billing the model each visit.
- **SOC 2 / ISO 27001 certification:** deferred post-GA (process, not code).
