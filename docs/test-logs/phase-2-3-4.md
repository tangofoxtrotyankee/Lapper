# Phases 2–4 Test Log

Follows `docs/phase-2-3-4-testing-guide.md`. Same recording rules as
earlier phases: exact output text, versions on any deviation, and **no
captured screen content or secrets in this file** — describing what
happened is fine, quoting screen text is not.

## Test session

| Field | Value |
|---|---|
| Date | |
| Tester | |
| Machine / OS | |
| .NET SDK version | |
| Node version | |
| Visual Studio version (if run from VS) | |
| Install method (VS run / MSIX artifact) | |
| Commit tested (`git rev-parse --short HEAD`) | |

## Results

| Step | Check | Actual result | Pass/Fail | Notes |
|---|---|---|---|---|
| P234-A1 | CI green; 13+34+15+27+21 desktop, 45 backend | | | |
| P234-A2 | Release (MSIX) workflow produces installable artifact | | | |
| P234-B1 | Backend `npm test` → 45 passed | | | |
| P234-B2 | `validate:contracts` passes | | | |
| P234-B3 | Cross-platform dotnet suites 34 + 15 passed | | | |
| P234-B4 | Windows dotnet suites 27 + 21 passed | | | |
| P234-C1 | Streamed orientation end-to-end (note time-to-first-text) | | | |
| P234-C2 | Coverage: Edge / Chrome / Notepad / Word / Outlook | | | |
| P234-C3 | Facts concrete and grounded in on-screen content | | | |
| P234-C4 | Excluded app blocked before capture | | | |
| P234-C5 | Password field focus → declined | | | |
| P234-C6 | Secrets redacted ([REDACTED:...] markers) | | | |
| P234-C7 | No OpenAI key in desktop package | | | |
| P234-C8 | Esc mid-stream / backend down / no key → all recoverable | | | |
| P234-C9 | Actions: fixed allowlist, copy/draft/ask work, confirmation | | | |
| P234-C10 | Read aloud on orientation and result | | | |
| P234-C11 | Backend logs metadata-only; no files with screen content | | | |

## Verdict — acceptance criteria

| Phase | Criterion | Pass/Fail |
|---|---|---|
| 2 | Useful text from Edge, Chrome, Outlook, Word, Notepad | |
| 2 | Source block IDs returned | |
| 2 | Excluded apps blocked before capture | |
| 2 | Password fields excluded | |
| 2 | Screenshot fallback: active window only | |
| 2 | Screenshots never written to disk | |
| 2 | Telemetry contains no captured text | |
| 3 | Streamed orientation on trigger | |
| 3 | No OpenAI key in desktop package | |
| 3 | Malformed model result rejected safely | |
| 3 | Timeout/cancel leaves app recoverable | |
| 3 | Prompt injection fixtures do not alter instructions | |
| 3 | Model responses not in operational logs | |
| 4 | Only allowlisted actions accepted | |
| 4 | Unknown model-proposed action rejected | |
| 4 | Local actions execute no arbitrary commands | |
| 4 | TTS reads orientation and full result | |
| 4 | Copy/draft require no cloud persistence | |

**Phases 2–4 accepted:** ☐ (all criteria pass)
