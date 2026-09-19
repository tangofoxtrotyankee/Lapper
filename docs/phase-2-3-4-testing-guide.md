# Phases 2–4 Testing Guide — Context Engine, AI Orientation, Core Actions

How to verify the acceptance criteria of `phases/02-context-engine.md`,
`phases/03-ai-orientation.md` and `phases/04-core-actions.md`. Record
results in `docs/test-logs/phase-2-3-4.md` — exact output, never a
paraphrase, and **never any captured screen text or secrets** (that rule is
itself part of what you're testing).

This wave turns the Phase 1 shell into the real product loop: press
Ctrl+Alt+L → local capture + redaction → backend → streamed orientation →
actions. It also adds an installable MSIX build.

## Prerequisites

- Everything from Phase 1 (Windows 11, VS 2026 or .NET 10 SDK).
- Node.js 24 LTS for the backend.
- An OpenAI API key (only needed for Tier C's AI steps; everything else
  works without one).

## Tier A — CI verification (~5 min)

**P234-A1.** GitHub → Actions → latest CI run on the branch: all jobs
green. The desktop job now runs five test suites — expected totals:
contracts 13, shell-core 34, api-client 18, privacy 30, context-windows 30.
Backend jobs run 72 tests on ubuntu and windows.

**P234-A2.** Actions → "Release (MSIX)" → Run workflow on this branch.
Expected: run goes green and produces an artifact `lapper-msix-x64`
containing `Lapper.msix`, `Lapper-Dev.cer`, `Install-Lapper.ps1`,
`INSTALL.md`, `SHA256SUMS.txt`.

## Tier B — automated tests on any machine (~10 min)

```
cd backend && npm ci && npm test          # expected: 72 passed
npm run validate:contracts                 # expected: all validations pass
cd .. && dotnet test desktop/Lapper.Shell.Core.Tests   # 34 passed
dotnet test desktop/Lapper.ApiClient.Tests             # 18 passed
```

On Windows additionally:

```
dotnet test desktop/Lapper.Privacy.Tests               # 30 passed
dotnet test desktop/Lapper.Context.Windows.Tests       # 30 passed
```

## Tier C — manual Windows verification (~40 min)

Setup: install the app (either run from VS as before, or use the release
artifact per `INSTALL.md`), then start the backend:

```
cd backend
npm ci
copy .env.example .env     (set OPENAI_API_KEY)
npm run build && npm start
```

**P234-C1. End-to-end orientation.**
Open a news article in Edge, press **Ctrl+Alt+L**. Expected: card appears
with "Reading this screen…", then a one-sentence orientation **streams in
word by word** within a few seconds, followed by a short summary, key facts
and action buttons. Record rough time-to-first-text.

**P234-C2. App coverage.**
Repeat P234-C1 in: Chrome (any webpage), Notepad (a paragraph of text),
Word (a document) and Outlook (an open email), if installed. Expected: a
relevant orientation in each; for a supplier-renewal-style email, the
orientation should surface the key change and deadline, not a generic
summary. Record which apps you tested and PASS/FAIL each.

**P234-C3. Facts cite sources.**
After an orientation, expand the card's facts. Expected: facts are concrete
(figures, dates) and consistent with the visible page — they come from
ranked text blocks with IDs, so nothing should reference content that
wasn't on screen.

**P234-C4. Excluded app is blocked before capture.**
Open Settings (tray) → add the executable of an app of your choice (e.g.
`notepad.exe`) to excluded apps → focus Notepad → Ctrl+Alt+L. Expected:
the card says this app is excluded and **no orientation happens**. Also
try Windows Security / an elevation (UAC) prompt if convenient: blocked by
default. Remove the exclusion afterwards and confirm Notepad works again.

**P234-C5. Password fields.**
Focus a browser login page with the cursor **in the password box**, press
Ctrl+Alt+L. Expected: Lapper declines (sensitive content message); no
orientation, nothing sent. With the cursor in the username box instead, it
may proceed — the password control itself must never be read.

**P234-C6. Redaction.**
Open Notepad, paste a fake secret of your own construction, e.g.
`password: hunter2` on one line and a made-up card number that passes Luhn
(`4111 1111 1111 1111`). Press Ctrl+Alt+L. Expected: any card content
shown/echoed contains `[REDACTED:...]` markers rather than the values.
(The backend never receives the raw values; you can verify in the backend
terminal that its logs show only request metadata — that's P234-C11.)

**P234-C7. No OpenAI key in the desktop package.**
Search the installed app folder (`C:\Program Files\WindowsApps\...Lapper...`
or your VS output folder) for `sk-`: `findstr /s /m "sk-" *.*`. Expected:
no API key anywhere; the key exists only in `backend/.env`. Also confirm
the app works with the backend URL pointing at your local server and fails
gracefully (next step) when it's down.

**P234-C8. Cancel / timeout / backend down.**
- Press Ctrl+Alt+L, then **Esc mid-stream**. Expected: card hides, app
  stays responsive, next trigger works.
- Stop the backend (`Ctrl+C`), press Ctrl+Alt+L. Expected: friendly
  "can't reach the Lapper backend" style error on the card — no crash, no
  hang. Restart the backend; next trigger works without restarting Lapper.
- Remove `OPENAI_API_KEY` from `.env`, restart backend, trigger. Expected:
  "AI is not configured" style message.

**P234-C9. Actions allowlist.**
After an orientation: Expected action buttons only ever come from
{Copy, Read aloud, Share, Draft a reply, Extract facts, Ask a question} —
the model cannot add novel buttons. Try:
- **Copy** → paste into Notepad: matches the card's readable text.
- **Draft** (on an email-like screen) → a draft **streams** into the card's
  result area; Copy the result works. Confirmation is required before any
  cloud action runs if marked as such.
- **Ask a question** → type a question about the screen → answer streams.

**P234-C10. Read aloud.**
Click **Read aloud** on an orientation and again on a generated result.
Expected: TTS reads the text through the default output device; triggering
a new session stops playback cleanly.

**P234-C11. No content in logs.**
While doing the steps above, watch the backend terminal. Expected: log
lines contain request IDs, routes, models, token counts, latency — **never**
screen text, questions or answers. Also check the app writes no screenshot
or text files: `%LOCALAPPDATA%\Packages\...Lapper...\` should contain
settings only.

## If something fails

Capture the exact step ID, what you saw vs expected, and any error text
(but never captured screen content); log it in
`docs/test-logs/phase-2-3-4.md`; paste the same into a Claude Code session
on this repo.

## Mapping to acceptance criteria

| Phase | Acceptance criterion | Steps |
|---|---|---|
| 2 | extracts useful text from Edge/Chrome/Outlook/Word/Notepad | P234-C1, C2 |
| 2 | returns source block IDs | P234-C3 (+ unit tests) |
| 2 | excluded apps blocked before capture | P234-C4 |
| 2 | password fields excluded | P234-C5 |
| 2 | screenshot fallback only captures active window | code review + C6 |
| 2 | screenshots never written to disk | P234-C11 (+ code review) |
| 2 | telemetry contains no captured text | P234-C11 |
| 3 | streamed orientation on trigger | P234-C1 |
| 3 | no OpenAI key in desktop package | P234-C7 |
| 3 | malformed model result rejected safely | backend tests |
| 3 | timeout/cancel recoverable | P234-C8 |
| 3 | prompt injection fixtures don't alter instructions | backend tests |
| 3 | model responses not in operational logs | P234-C11 |
| 4 | only allowlisted actions accepted | P234-C9 (+ tests) |
| 4 | unknown model-proposed action rejected | P234-C9 (+ tests) |
| 4 | local actions execute no arbitrary commands | code review |
| 4 | TTS reads orientation and results | P234-C10 |
| 4 | copy/draft need no cloud persistence | P234-C9 (+ code review) |
