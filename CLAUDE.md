# {{PROJECT_NAME}}

## Stack
{{FLAVOR}}. .NET 10 Native AOT. See `docs/architecture/SDD.md`.

## Standard flow
analyze -> read docs/ -> read Directory.Build.props -> plan -> work.

## Conventions
- Image tags: `sha-{SHORT_SHA}` (test), `prod-{SHA}` (prod).
- Release tags: `YYYYMMDD_{{PROJECT_NAME}}_Release`.
- Secrets: never in `src/`. See `.env.example`.
- Tests: progressive pyramid. E2E smoke on PR, integration on main.
- Commits: Conventional Commits (feat/fix/chore/docs/refactor/test/ci).
- Branches: `main` protected. Squash-merge via PR.

## Deploy target
{{DEPLOY_TARGET}}

## Commands
- `/ship` — full pre-push sequence (format + test + build + secret-scan)
- `/deploy-test` — trigger test deploy
- `/add-adr` — new DECISIONS.md entry
- `/bump-image` — roll image tag in compose/helm
- `/smoke` — run E2E smoke locally

## Do not
- Add static AWS keys. Use OIDC.
- Skip the secret-scan pre-commit hook.
- Bump major deps without an ADR.
- Disable `TreatWarningsAsErrors`.
- Add telemetry SDKs (ApplicationInsights/Sentry/Datadog/etc). Banned by CI.
