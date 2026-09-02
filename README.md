# Lapper Build Pack

Lapper is a Windows-first AI screen understanding and action assistant.

Core proposition:

> Lapper understands what is on your screen, tells you what matters, and helps you do the next thing.

This repository pack is designed to be handed directly to Claude Code before implementation begins.

## Start here

1. Read `CLAUDE.md` completely.
2. Read `docs/01-product-brief.md`.
3. Read `docs/02-architecture.md`.
4. Read `docs/03-security-threat-model.md`.
5. Read `phases/00-foundation.md`.
6. Do not begin later phases until the current phase acceptance criteria pass.

## Locked MVP stack

- Windows client: C# 14, .NET 10, WinUI 3, Windows App SDK
- Package: MSIX
- Context: UI Automation -> OCR -> screenshot fallback
- Backend: Node.js 24 LTS, TypeScript, Fastify 5
- Database: Neon PostgreSQL + Drizzle ORM
- Authentication: Auth0 OIDC + Authorization Code with PKCE
- AI: OpenAI Responses API behind internal ModelGateway
- Default AI route: GPT-5.6 Terra
- Cheap route: GPT-5.6 Luna
- Deep route: GPT-5.6 Sol, user initiated only
- Billing later: Stripe
- Hosting: Railway EU region
- Desktop local settings: SQLite
- Secrets/tokens: Windows Credential Locker / DPAPI where required

## Non-negotiables

- Lapper never continuously watches the user's screen in MVP.
- Screen analysis occurs only after explicit user action.
- Prefer structured UI text over screenshots.
- Screenshots, OCR text, prompts and model answers are not stored in Lapper cloud systems by default.
- The desktop app never contains OpenAI, Stripe or database secrets.
- AI never executes arbitrary OS commands.
- All state-changing actions require allowlisted types and explicit policy checks.
- No vector database, Redis, microservices, autonomous agent framework or browser automation in MVP.

## Repository layout

- `desktop/` — Windows client solution (`Lapper.slnx`, WinUI 3 / .NET 10).
  Build on Windows: `dotnet build desktop/Lapper.slnx -p:Platform=x64`.
  Cross-platform contract tests: `dotnet test desktop/Lapper.Contracts.Tests`.
- `backend/` — Fastify 5 / TypeScript backend. `npm ci && npm test`; start
  with `npm run dev` (no secrets required; `/health/live` returns 200).
- `contracts/` — OpenAPI 3.1 document, orientation JSON schema and shared
  good/bad fixtures. Validate: `npm run validate:contracts` (from `backend/`).
- `docs/adr/` — architecture decision records (see `CLAUDE.md` ADR policy).
- `.github/workflows/` — CI (backend, contracts, Windows desktop build,
  gitleaks secret scan, dependency review) and CodeQL.

## Phases

- Phase 0: repo, tooling, contracts, build pipeline
- Phase 1: native Windows shell and floating control
- Phase 2: local screen context engine
- Phase 3: AI orientation loop
- Phase 4: core actions and local TTS
- Phase 5: accounts, database and usage controls
- Phase 6: security hardening and privacy controls
- Phase 7: Laps and product configuration
- Phase 8: billing, telemetry and private beta

See `phases/` for exact scope and acceptance criteria.
