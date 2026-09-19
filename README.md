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

## Installing / running it

See `INSTALL.md`: install the MSIX from the "Release (MSIX)" workflow
artifact, run the backend with your OpenAI key, press **Ctrl+Alt+L**.

## Repository layout

- `desktop/` — Windows client solution (`Lapper.slnx`, WinUI 3 / .NET 10):
  shell (tray, pill, card), context engine (UIA/OCR/capture), privacy
  (exclusions, redaction), actions, API client, shared contracts.
  Build on Windows: `dotnet build desktop/Lapper.slnx -p:Platform=x64`.
  Cross-platform tests: `dotnet test desktop/Lapper.Shell.Core.Tests`
  (also `Lapper.Contracts.Tests`, `Lapper.ApiClient.Tests`).
- `backend/` — Fastify 5 / TypeScript backend: `/v1/context/orient` and
  `/v1/context/action` (SSE streaming), ModelGateway → OpenAI Responses
  API (`store:false`). `npm ci && npm test`; start with `npm run dev`
  (boots without secrets; `/health/live` returns 200, AI endpoints need
  `OPENAI_API_KEY` in `.env`).
- `contracts/` — OpenAPI 3.1 document, orientation/orient-request/
  action-request JSON schemas and shared good/bad fixtures. Validate:
  `npm run validate:contracts` (from `backend/`).
- `docs/adr/` — architecture decision records (see `CLAUDE.md` ADR policy).
- `docs/phase-*-testing-guide.md` — how to verify each phase by hand;
  record results in `docs/test-logs/`.
- `.github/workflows/` — CI (backend, contracts, Windows desktop build +
  tests, gitleaks secret scan, dependency review), CodeQL, and the manual
  Release (MSIX) packaging workflow.

## Phases

- Phase 0: repo, tooling, contracts, build pipeline — **done**
- Phase 1: native Windows shell and floating control — **done**
- Phase 2: local screen context engine — **built, awaiting manual acceptance**
- Phase 3: AI orientation loop — **built, awaiting manual acceptance**
- Phase 4: core actions and local TTS — **built, awaiting manual acceptance**
- Phase 5: accounts, database and usage controls
- Phase 6: security hardening and privacy controls
- Phase 7: Laps and product configuration
- Phase 8: billing, telemetry and private beta

See `phases/` for exact scope and acceptance criteria.
