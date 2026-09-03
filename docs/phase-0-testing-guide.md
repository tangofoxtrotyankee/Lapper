# Phase 0 Testing Guide

How to verify the Phase 0 acceptance criteria yourself, step by step.

Record your results in `docs/test-logs/phase-0.md` as you go — one row per
step. When a result differs from what's written here, copy the **exact**
output text into the log (not a paraphrase).

Pick the tier(s) that match the machine you're on. Tier A needs nothing
installed. Tier B works on any computer with Node.js. Tier C needs a Windows
PC. Doing A + B + C covers every acceptance criterion.

> Privacy reminder (CLAUDE.md): never paste real screen content, personal
> data or secrets into test logs, fixtures or issues.

---

## Tier A — verify from GitHub only (~10 minutes, no installs)

**A1. CI is green.**
Open the repository on GitHub → **Actions** tab → the **CI** workflow →
the most recent run on branch `claude/phase-0-setup-testing-rkc2cq`.

- Expected: conclusion **success**, with these jobs all green:
  - *Backend (lint, typecheck, tests)*
  - *Contracts (schema + fixtures + OpenAPI 3.1)*
  - *Desktop (Windows solution build + tests)*
  - *Secret scanning (gitleaks)*
- Note: *Dependency review* is **skipped on push-triggered runs** but
  **executes on pull-request runs**. On PR runs it requires the repository's
  **Dependency graph** feature: GitHub → Settings → Advanced Security /
  Security → "Dependency graph" → Enable (free for public repositories).
  If it fails with `Dependency review is not supported on this repository.
  Please ensure that Dependency graph is enabled`, that toggle is off —
  enable it and re-run the failed job.
- Record: the run number, the commit SHA it ran on, and pass/fail per job.

**A2. The desktop build really happened on Windows.**
Click the *Desktop (Windows solution build + tests)* job and skim the log.

- Expected in the log: `Build succeeded` for the solution, and a test line
  ending `Failed: 0, Passed: 13, Skipped: 0, Total: 13`.
- Record: the passed/failed test counts you see.

**A3. No secrets are needed or present.**
Browse `.env.example` (repo root) and `backend/.env.example` on GitHub.

- Expected: placeholder values only (`REPLACE_ME`, ports, model names) —
  no real keys, passwords or connection strings anywhere.
- Also confirm the *Secret scanning (gitleaks)* job in A1 reported
  `no leaks found`.
- Record: pass/fail, plus anything that looks like a real credential
  (there should be nothing).

---

## Tier B — backend and contracts on any machine (~15 minutes)

**Prerequisite:** Node.js 24 LTS from https://nodejs.org (Node 22 also
works — record which version you used). Check with:

```
node --version
```

**B1. Clean clone and install.**

```
git clone https://github.com/tangofoxtrotyankee/Lapper.git
cd Lapper
git checkout claude/phase-0-setup-testing-rkc2cq
cd backend
npm ci
```

- Expected: install completes with `found 0 vulnerabilities` and no errors.

**B2. Automated tests.**

```
npm test
```

- Expected: `Test Files  2 passed (2)` and `Tests  15 passed (15)`.
- Record: the exact counts.

**B3. Lint, types and formatting.**

> Windows note: if `format:check` reports style issues in many files, your
> clone probably predates the repository's `.gitattributes` (which forces LF
> line endings on checkout). Fix by re-checking out:
> `git rm -rf --cached . && git reset --hard` — or simply re-clone.

```
npm run lint
npm run typecheck
npm run format:check
```

- Expected: each command finishes with no errors ( `format:check` prints
  `All matched files use Prettier code style!`).

**B4. Contract schema validates good and bad fixtures.**

```
npm run validate:contracts
```

- Expected: `ok:` lines for `openapi.yaml`, the schema, 3 `fixtures/valid/`
  files and 7 `fixtures/invalid/` files, ending with
  `All contract validations passed.`
- Record: the valid/invalid fixture counts.

**B5. Server starts with no secrets and health checks return 200.**
First confirm there is **no** `.env` file in `backend/` (that's the point:
Phase 0 must run without secrets). Then:

```
npm run build
npm start
```

- Expected: a log line showing the server listening on
  `http://127.0.0.1:3000`.

In a web browser (or a second terminal) open:

- `http://127.0.0.1:3000/health/live` → expected `{"status":"live"}`
- `http://127.0.0.1:3000/health/ready` → expected `{"status":"ready"}`

Stop the server with `Ctrl+C`. Record: both responses and (if you used
a terminal with `curl -i`) the `200` status codes.

---

## Tier C — Windows desktop build (~30 minutes including installs)

**Prerequisite:** a Windows 10/11 PC with the .NET 10 SDK. Install from an
elevated terminal:

```
winget install Microsoft.DotNet.SDK.10
```

Then close and reopen the terminal and check: `dotnet --version` → expect
a `10.0.x` version. Visual Studio is **not** required for C1–C2.

**C1. Build the whole Windows solution from a clean clone.**
From the repository root (clone as in B1 if you haven't):

```
dotnet build desktop/Lapper.slnx -p:Platform=x64
```

- Expected: `Build succeeded.` with 0 errors. The first run downloads
  packages — the initial restore alone can take ~3 minutes on normal
  broadband. No certificate or secret is asked for.
- Record: build succeeded/failed, elapsed time, any warnings.

**C2. Desktop contract tests.**

```
dotnet test desktop/Lapper.Contracts.Tests
```

- Expected: `Passed!` with `Failed: 0, Passed: 13`.
- Record: the counts.

**C3 (optional). Launch the app.**
Open `desktop/Lapper.slnx` in **Visual Studio 2026** (Community is fine)
with the **WinUI application development** workload — .NET 10 is not
supported by Visual Studio 2022. Set `Lapper.Shell` as startup, platform
`x64`, and run. The repo includes `Properties/launchSettings.json` (MSIX
package profile) and marks the project for deployment in the solution; if
Visual Studio still reports "The project needs to be deployed", tick
**Deploy** for `Lapper.Shell` / `x64` in Build → Configuration Manager and
run again.

- Expected: an empty window titled **Lapper** showing "Phase 0 foundation
  build. Screen understanding arrives in later phases." That is the entire
  Phase 0 app — no tray icon, no shortcut, no capture, no AI. Anything more
  would be a scope violation worth recording.

---

## If something fails

1. Don't retry blindly — capture first: the full command you ran, the
   complete error output (copy/paste, not a screenshot of text), your OS,
   and `node --version` / `dotnet --version`.
2. Add a row to `docs/test-logs/phase-0.md` marked **FAIL** with that
   detail.
3. Paste the same detail into a Claude Code session on this repository and
   ask it to diagnose. Include which guide step (e.g. "B4") failed.
4. A step that fails on your machine but passes in CI usually means an
   environment difference — record both facts; that contrast is the most
   useful diagnostic clue.

## Mapping steps to acceptance criteria

| Acceptance criterion (phases/00-foundation.md) | Verified by |
|---|---|
| Clean clone builds Windows solution | C1 (or A2 via CI) |
| Backend starts locally, `/health/live` returns 200 | B5 |
| All automated tests pass | B2 + C2 (or A1/A2) |
| No production secrets required | A3 + B5 |
| CI runs desktop build, backend checks, schema validation | A1 |
| Orientation schema validates good/bad fixtures | B4 + C2 |

When every row of the log is filled in, complete the verdict section at the
bottom of the log — that sign-off is the gate for starting Phase 1.
