# Phase 0 Implementation Checklist

Maps every Phase 0 task and acceptance criterion in `phases/00-foundation.md`
to concrete deliverables in this repository.

## Acceptance criteria mapping

### AC1 — clean clone can build Windows solution
- `desktop/Lapper.sln` containing:
  - `Lapper.Shell` — WinUI 3 packaged app (net10.0-windows, Windows App SDK pinned)
  - `Lapper.Context.Windows`, `Lapper.Privacy`, `Lapper.Actions`, `Lapper.ApiClient` — net10.0-windows class libraries
  - `Lapper.Contracts` — net10.0 (cross-platform) shared DTO/schema library
  - `Lapper.Contracts.Tests` — xUnit test project (cross-platform)
- No secrets or machine-specific paths required for restore/build.
- Verified by the CI `desktop` job on `windows-latest` (this development
  environment is Linux; only the cross-platform projects build locally).

### AC2 — backend starts locally and `/health/live` returns 200
- `backend/` Fastify 5 + TypeScript strict app.
- `GET /health/live` → 200 `{ "status": "live" }`
- `GET /health/ready` → 200 `{ "status": "ready" }` (no dependencies in Phase 0)
- Bounded request bodies configured; no request-content logging.
- Verified by an integration test using `fastify.inject` and by starting the
  server locally.

### AC3 — all automated tests pass
- Backend: vitest unit + integration tests (health routes, schema validation).
- Desktop: `Lapper.Contracts.Tests` validating the embedded orientation schema
  against the shared fixtures.
- Verified locally (backend + cross-platform desktop tests) and in CI.

### AC4 — no production secrets required for basic build
- `.env.example` placeholders only; backend boots with built-in defaults and no
  `.env` file. No key material anywhere in the repo. `.gitignore` already
  excludes env files, keys and certificates.

### AC5 — CI runs desktop build, backend typecheck/tests and schema validation
- `.github/workflows/ci.yml`:
  - `backend` job (ubuntu, Node 24): install → lint → typecheck → test
  - `contracts` job (ubuntu): validate orientation schema + fixtures + OpenAPI 3.1 document
  - `desktop` job (windows-latest, .NET 10): restore/build solution + run tests
- `.github/workflows/codeql.yml` — CodeQL for JS/TS and C# (security scanning).
- Secret scanning via gitleaks job; dependency scanning via dependency review
  and `npm audit`.

### AC6 — orientation schema validates known good/bad fixtures
- `contracts/orientation.schema.json` (existing, unchanged) is the single
  source of truth.
- `contracts/fixtures/valid/*.json` and `contracts/fixtures/invalid/*.json`.
- Backend: Ajv 2020-12 validation with tests asserting valid fixtures pass and
  each invalid fixture fails.
- Desktop: schema + fixtures embedded into `Lapper.Contracts`; JsonSchema.Net
  validation exercised by `Lapper.Contracts.Tests` with the same assertions.

## Phase 0 task mapping

| Phase 0 task | Deliverable |
|---|---|
| solution/repo structure | `desktop/`, `backend/`, `contracts/`, `docs/adr/` |
| WinUI 3 packaged desktop app | `desktop/Lapper.Shell` (MSIX-packaged, single-project) |
| project boundaries from CLAUDE.md | six desktop projects as specified |
| Node/TypeScript/Fastify backend | `backend/` |
| TypeScript strict mode | `backend/tsconfig.json` (`strict: true` + extra strictness flags) |
| formatting/linting | ESLint + Prettier (backend), `.editorconfig` (repo-wide incl. C#) |
| test projects | `backend` vitest, `Lapper.Contracts.Tests` xUnit |
| OpenAPI/schema package | `contracts/` with `openapi.yaml` (3.1) + JSON schema + fixtures |
| orientation schema in both stacks | Ajv wiring (backend) + embedded resource (desktop) |
| local environment templates | `.env.example` (root) + `backend/.env.example` |
| GitHub Actions build/test | `.github/workflows/ci.yml` |
| secret/dependency scanning | gitleaks + dependency review + `npm audit` + CodeQL |
| ADR folder | `docs/adr/` with template and README |

## Manual verification

Human-runnable verification steps live in `docs/phase-0-testing-guide.md`;
record outcomes in `docs/test-logs/phase-0.md`.

## Prerequisites / conflicts identified

1. **This development environment is Linux.** The WinUI 3 solution cannot be
   compiled here; the desktop build acceptance criterion is verified by the CI
   `desktop` job on `windows-latest`. Cross-platform projects
   (`Lapper.Contracts` + tests) are built and run locally as a partial check.
2. **Toolchain versions pinned deliberately:** TypeScript is pinned to 5.9.x
   (not the current `latest` 7.x) because typescript-eslint's supported range
   is `<6.1.0`; Windows App SDK is pinned to the latest stable 1.8 release
   rather than 2.x to stay on the long-known-stable project shape. Either can
   be revisited in a later phase — neither is a locked-architecture change.
3. **No conflicts found** between `phases/00-foundation.md`, `CLAUDE.md` and
   the architecture docs. Out-of-scope items (OpenAI, capture, UIA, auth,
   database) are excluded from Phase 0 code.
