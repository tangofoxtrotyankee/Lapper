# Phases 2–4 Implementation Checklist

Maps `phases/02-context-engine.md`, `phases/03-ai-orientation.md` and
`phases/04-core-actions.md` to concrete deliverables. The three phases
shipped as one wave because they form a single end-to-end loop:
capture → redact → orient → act.

## Phase 2 — local context engine

| Acceptance criterion | Deliverable | Verified by |
|---|---|---|
| extracts useful text from Edge, Chrome, Outlook, Word and Notepad | `UiaExtractor` (cached bulk UIA query, selection via TextPattern, document visible ranges, Chromium warm-up retry) + `AppCategoryMap` | manual guide P234-C2 |
| returns source block IDs | `BlockRanker` assigns deterministic `b1..bn`; facts cite `sourceBlockIds` end to end | unit tests + P234-C3 |
| excluded apps return blocked status before capture | `ExclusionPolicy` (fail-closed, built-ins + user list) evaluated in `ContextAcquisitionService` **before** any UIA/OCR/screenshot work | unit tests + P234-C4 |
| password fields are excluded | `SensitiveContextGate` + focused-password check in `UiaExtractor` before any text read | unit tests + P234-C5 |
| screenshot fallback captures only active target window | `WindowCapture` uses `PrintWindow`/`BitBlt` on the target HWND only; OCR only when UIA text is sparse | code review + P234-C6 |
| screenshots are never written to disk | `CapturedFrame` is memory-only, pixels zeroed on dispose; no file APIs in the capture path | code review + CI |
| telemetry contains no captured text | `AcquisitionTimings`/`Flags` are metadata-only; redactor emits pattern names, never matches | unit tests (log canary) |

Components: `Lapper.Context.Windows` (probe, capture thread, UIA client and
extractor, GDI window capture, OCR fallback, block normalizer/ranker),
`Lapper.Privacy` (exclusions, secret redaction incl. Luhn, screenshot
policy, sensitive-context gate). Interop via Microsoft.Windows.CsWin32
(pinned, compile-time source generator — no runtime dependency).

## Phase 3 — AI orientation loop

| Acceptance criterion | Deliverable | Verified by |
|---|---|---|
| pressing Lapper on a supported screen shows streamed orientation | `LapperOrchestrator` → `LapperApiClient` (SSE) → backend `/v1/context/orient` → OpenAI Responses (`store:false`); deltas stream into the card via `OrientationDeltaExtractor` | P234-C1/C2 |
| no OpenAI key exists in desktop package | desktop only knows the backend URL; the key lives in `backend/.env` | code review + P234-C7 |
| malformed model result is rejected safely | backend re-validates final output against `orientation.schema.json` (Ajv) → `MODEL_OUTPUT_INVALID` error event; client shows a friendly error | backend tests |
| timeout/cancel leaves app recoverable | per-session CTS; Esc/retrigger cancels; gateway timeout maps to `MODEL_TIMEOUT` | backend + client tests, P234-C8 |
| prompt injection fixtures do not alter Lapper instructions | screen content sent only as JSON-encoded `SCREEN_CONTEXT (untrusted data)` user input, never instructions; 6 injection fixtures asserted | `test/prompt-injection.test.ts` |
| model responses are not written to operational logs | metadata-only logging + pino sink canary test | `test/log-content.test.ts` |

Backend: Fastify 5 SSE routes, `ModelGateway` abstraction, OpenAI Responses
adapter (strict structured output, `store:false`), model router
(luna/terra/sol per CLAUDE.md), bounded bodies, error envelopes.

## Phase 4 — core actions

| Acceptance criterion | Deliverable | Verified by |
|---|---|---|
| only allowlisted actions are accepted | `ActionPolicy` allowlist (local: copy_text/read_aloud/share_text; cloud: draft_text/extract_facts/ask_question); backend schema enum rejects everything else | unit tests both stacks |
| model-proposed unknown action is rejected | `ActionPolicy.FilterAllowed` drops unknown suggestions before the card renders them; dispatcher re-validates | unit tests + P234-C9 |
| local actions do not execute arbitrary commands | local actions are clipboard + TTS only; no shell/URL execution paths exist | code review |
| TTS can read orientation and full generated result | `SpeechService` (in-memory synthesis stream, no temp files) reads card text and results | P234-C10 |
| copy/draft actions require no cloud persistence | copy/share are fully local; draft streams through the stateless backend (`store:false`, nothing persisted) | code review + backend tests |

## Installability (wave extra)

- `.github/workflows/release.yml` — manual (workflow_dispatch) build of a
  signed self-contained MSIX bundle: `Lapper.msix`, `Lapper-Dev.cer`,
  `Install-Lapper.ps1`, `INSTALL.md`, `SHA256SUMS.txt`.
- `INSTALL.md` — end-user install/run instructions (app + backend + key).

## Testing

- Backend: 45 vitest tests (routes, SSE, gateway, router, strict schema,
  injection fixtures, log canary) on ubuntu + windows CI lanes.
- Desktop: 13 contracts + 34 shell-core + 15 api-client tests
  (cross-platform) and 27 privacy + 21 context-windows tests
  (windows-latest CI).
- Manual: `docs/phase-2-3-4-testing-guide.md`, recorded in
  `docs/test-logs/phase-2-3-4.md`.
