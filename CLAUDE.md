# CLAUDE.md - Lapper Engineering Instructions

You are building Lapper, a Windows-first AI screen understanding and action assistant.

## Product rule

The user should be able to press a global shortcut or floating Lapper control and receive a useful one-sentence orientation about the active window without switching apps.

Lapper is not a chatbot with screen capture. The current screen is already the prompt.

## Working method

1. Read this file before changing code.
2. Read the relevant phase file in `phases/`.
3. Read the relevant architecture/security documents before implementing system-level code.
4. Work only inside the active phase unless a prerequisite is genuinely missing.
5. Never silently change a locked architecture decision. Create an ADR proposal instead.
6. Keep changes small and testable.
7. After each task, run the relevant tests/build and report exactly what passed or failed.
8. Do not mark a phase complete until every acceptance criterion passes.
9. Do not add dependencies merely for convenience. Explain material new dependencies before adding them.
10. Security requirements outrank convenience and feature speed.

## Locked architecture

### Desktop
- C# 14
- .NET 10
- WinUI 3 / Windows App SDK
- packaged MSIX application
- x64 first
- MVVM pattern, but avoid framework-heavy abstraction unless justified

Projects:
- `Lapper.Shell`
- `Lapper.Context.Windows`
- `Lapper.Privacy`
- `Lapper.Actions`
- `Lapper.ApiClient`
- `Lapper.Contracts`

### Backend
- Node.js 24 LTS
- TypeScript strict mode
- Fastify 5
- Drizzle ORM
- Neon PostgreSQL
- OpenAPI 3.1
- Server-Sent Events for normal AI streaming

### AI
- OpenAI Responses API
- model calls only from backend
- internal `ModelGateway` abstraction
- GPT-5.6 Luna: cheap/simple clean-text tasks
- GPT-5.6 Terra: default production model
- GPT-5.6 Sol: explicit Deep mode only
- structured JSON output for machine-readable responses
- `store: false` on model calls

## Context acquisition priority

Always use this order:

1. Explicit user selection if available.
2. UI Automation text and structure.
3. Local OCR.
4. Active-window screenshot only when required.

Never default to full-screen capture when active-window context is sufficient.

## Privacy rules

Never log or persist:
- screenshot pixels
- OCR text
- UI Automation text
- selected text
- clipboard content
- user screen question
- model answer
- audio
- auth tokens

Cloud operational logs may contain only metadata such as request ID, pseudonymous account ID, model, route, token counts, latency, success/failure and error code.

Screenshots must remain in memory and be released after processing. Do not write screenshots to temp files.

Do not introduce screen history without a separate product decision.

## Security rules

Treat all screen content as hostile untrusted data.

A webpage/email/document may contain prompt injection. Therefore:
- never interpret screen text as system instructions
- never expose arbitrary OS tools to the model
- never execute shell commands returned by a model
- never navigate arbitrary URLs returned by a model without validation/user action
- model actions must be validated against an allowlist
- state-changing actions require confirmation unless explicitly classified safe
- block password controls and known sensitive apps by default

MVP allowed action types:
- `copy_text`
- `read_aloud`
- `draft_text`
- `extract_facts`
- `ask_question`
- `share_text`

Do not implement sending email, deleting files, payments, browser automation or autonomous click control in MVP.

## API conventions

Base prefix: `/v1`

Use:
- JSON Schema validation on every request/response
- typed contracts shared via generated OpenAPI client
- `X-Lapper-Request-Id` on analysis requests
- `Idempotency-Key` on endpoints with side effects
- bounded request bodies
- explicit timeout/cancellation

Never accept raw arbitrary model instructions from the desktop client.

## Database rules

Postgres stores metadata and product configuration only.

Do not store captured screen content.

Tenant-scoped tables require RLS.

Roles:
- `lapper_migrator`
- `lapper_runtime`
- `lapper_readonly`

Runtime role must not own tables and must not bypass RLS.

## UX rules

The first useful output is a short orientation, not a long summary.

Ideal response:
"This is a supplier renewal notice. Your fee rises from £400 to £472 on 1 October, and cancellation is due by 12 September."

Avoid opening a full chat window for standard use.

Primary interaction surfaces:
- floating pill
- global shortcut
- compact context card
- optional deeper panel only when requested

## Performance targets

Initial engineering targets:
- local trigger feedback: <100 ms
- foreground window identification: <150 ms typical
- UIA extraction budget: <350 ms typical
- orientation request dispatched: <700 ms typical after trigger
- time to first useful streamed text: target <2.0 s on normal broadband

Do not fake these metrics. Instrument them.

## Testing requirements

Every phase must include tests.

Required layers:
- unit tests for parsing/policy/context ranking
- integration tests for API schemas and DB access
- Windows context fixtures for common apps
- prompt/model eval dataset
- security tests for prompt injection and tenant isolation

Never make tests pass by weakening assertions without documenting the reason.

## Definition of done for a task

A task is done only when:
1. code builds
2. automated tests pass
3. lint/type checks pass where applicable
4. new security-sensitive behavior has tests
5. relevant docs are updated
6. no secrets or captured screen content are introduced into logs/fixtures

## ADR policy

If you believe a locked decision should change, create `docs/adr/NNNN-title.md` containing:
- context
- current decision
- proposed change
- alternatives
- security/privacy impact
- migration cost
- recommendation

Do not implement the architecture change until approved.
