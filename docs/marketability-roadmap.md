# OmniSift — Marketability Roadmap

Ten upgrades that move OmniSift from internal prototype to shippable product.

| # | Feature | Description | Status |
|---|---------|-------------|--------|
| 1 | Structured citations that render | Every answer chunk links back to its source document/URL with inline footnotes visible in the UI, not raw JSON | In progress |
| 2 | One-click exportable cited report | Download a formatted PDF/DOCX with full citation list; shareable outside the app | Deferred |
| 3 | Segment-specific onboarding + sample data | Role-aware first-run flow (genealogist / journalist / legal) pre-loads a curated sample corpus so users see value in under 2 minutes | Deferred |
| 4 | Auth + self-serve signup + multi-user | User registration, JWT-gated API, per-tenant data isolation wired end-to-end; Stripe billing hooks | In progress (partial — JWT + RLS exist; signup UI + billing TBD) |
| 5 | Streaming + markdown rendering | Stream tokens from the LLM to the Blazor UI; render markdown (bold, lists, code blocks) instead of raw text | In progress |
| 6 | Hybrid search + reranking + confidence badges | Combine vector + BM25 full-text search; rerank results; show per-claim confidence scores | In progress |
| 7 | Trust/compliance: audit log + PII flagging + GDPR delete/export | Immutable per-tenant audit log; automatic PII detection in ingested docs; right-to-erasure endpoint | In progress |
| 8 | Broaden ingestion: DOCX/email/OCR + async pipeline | Ingest Word docs and raw email (`.eml`/`.mbox`); OCR for scanned PDFs; async background queue so large uploads don't block the UI | In progress (DOCX/email pipeline started; OCR deferred) |
| 9 | Entity timelines & relationship graphs | Automatically extract named entities (people, orgs, dates) and render an interactive relationship graph + timeline | Deferred |
| 10 | Pricing/packaging + hosted demo + deploy story | Public pricing tiers, hosted demo instance, production Docker Compose + CI deploy pipeline | In progress |

---

## Legend

- **Done** — shipped and wired end-to-end
- **In progress** — active development this cycle
- **Deferred** — backlog; not blocked, just not prioritised yet

---

## Notes

- Item 4 (auth): JWT issuing and RLS are in place; self-serve signup UI, email verification, and Stripe integration are the remaining gaps.
- Item 8 (ingestion): DOCX parsing and email ingest are underway; OCR (Tesseract / AWS Textract) is deferred.
- Item 2 (export): Depends on item 1 (citations) and item 5 (markdown) being stable first.
- Item 9 (entity graph): Likely needs a graph store (Neo4j or pgvector adjacency) — architectural decision deferred.
- SOC 2 / ISO 27001 certification: deferred post-GA.
