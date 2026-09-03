# Phase 0 Test Log

Follows `docs/phase-0-testing-guide.md`.

Recording rules:
- Copy **exact** output text into "Actual result" — never paraphrase errors.
- If a result deviates at all, also record tool versions in Notes.
- Screenshots are optional; if used, name the file after the step
  (e.g. `phase0-B5-health-live.png`) and reference it in Notes.
- Never record real screen content, personal data or secrets.

---

## Session 1 — manual verification (Sam, Windows)

| Field | Value |
|---|---|
| Date | 2026-09-03 |
| Tester | Sam |
| Machine / OS | SamOfficePC, Windows |
| Node version | v24.18.0 |
| .NET SDK version | 10.0.400 |
| IDE | Visual Studio 2026 Community (WinUI workload) |
| Commit tested | 41b70d8 |

| Step | Check | Actual result | Pass/Fail | Notes |
|---|---|---|---|---|
| A1 | CI run green | CI run #5 (PR): Backend, Contracts, Desktop, Secret scanning passed; Dependency review FAILED: `Error: Dependency review is not supported on this repository. Please ensure that Dependency graph is enabled` | FAIL | Repo configuration, not app code. Guide wrongly said the job is always skipped — it runs on PR-triggered runs. |
| A2 | CI desktop job detail | `Build succeeded. 0 Warning(s) 0 Error(s)`; `Passed! - Failed: 0, Passed: 13, Skipped: 0, Total: 13, Duration: 221 ms - Lapper.Contracts.Tests.dll (net10.0)` | PASS | |
| A3 | No secrets / gitleaks | Root `.env.example` placeholders only incl. `REPLACE_ME`; `backend/.env.example` dev host/port only; gitleaks job passed | PASS | |
| B1 | `npm ci` | `added 181 packages, and audited 182 packages in 8s` … `found 0 vulnerabilities` | PASS | |
| B2 | `npm test` | `Error: ENOENT: no such file or directory, scandir 'C:\C:\Users\tfysa\Documents\GitHub\Lapper\contracts\fixtures\valid'` — `Test Files 1 failed \| 1 passed (2)`, `Tests 3 passed (3)` | FAIL | Windows-only: `new URL(...).pathname` yields `/C:/...`, mangled by `join()`. `validate:contracts` passed on the same machine (it uses `fileURLToPath`). |
| B3 | lint / typecheck / format:check | lint PASS; typecheck PASS; format:check FAIL: `Code style issues found in 14 files.` (all backend text files) | FAIL | CRLF checkout on Windows (no `.gitattributes` at time of test) vs Prettier's LF rule. No `--write` was run, preserving test state. |
| B4 | `validate:contracts` | `ok: openapi.yaml is valid OpenAPI 3.1`; schema compiles; 3 valid + 7 invalid fixtures ok; `All contract validations passed.` | PASS | |
| B5 | Server + health, no `.env` | No `.env` present; build+start PASS; log `Server listening at http://127.0.0.1:3000`; `/health/live` → `{"status":"live"}`; `/health/ready` → `{"status":"ready"}` | PASS | |
| C1 | Solution build | `Build succeeded in 212.8s` (initial restore 194.7s) | PASS | |
| C2 | Desktop tests | `Test summary: total: 13, failed: 0, succeeded: 13, skipped: 0, duration: 0.9s` | PASS | |
| C3 | Launch Lapper.Shell | Launched OK after setup: window title `Lapper`, message `Phase 0 foundation build. Screen understanding arrives in later phases.` No extra functionality. | PASS | Setup friction: VS 2022 cannot target .NET 10 → VS 2026 installed; `Properties\launchSettings.json` missing (hand-created with MsixPackage profile); Deploy had to be enabled in Configuration Manager for x64. |

**Session 1 outcome:** functionally working; formal acceptance blocked by
A1, B2, B3.

---

## Session 2 — fix verification (Claude Code)

Fixes applied after Session 1:

- **B2**: `backend/test/orientation-schema.test.ts` now uses
  `fileURLToPath()` instead of `URL.pathname`.
- **B3**: root `.gitattributes` added (`* text=auto eol=lf`, binaries
  excluded) so Windows checkouts get LF; guide gains a re-checkout note for
  older clones.
- **A1**: guide corrected (job runs on PR CI and needs the Dependency graph
  repo setting, which Sam is enabling); workflow unchanged.
- **CI**: new `Backend tests (Windows)` job on `windows-latest` proves the
  B2 fix on real Windows on every run.
- **C3 friction**: `Properties/launchSettings.json` committed; `Lapper.Shell`
  marked `<Deploy />` in `Lapper.slnx`; guide now says Visual Studio 2026.

| Field | Value |
|---|---|
| Date | 2026-09-03 |
| Tester | Claude Code (Linux container + GitHub Actions) |
| Commit tested | 41002d7 (rebased onto main as 5ebe974 after PR #1 merged) |

| Step | Check | Actual result | Pass/Fail | Notes |
|---|---|---|---|---|
| B2 | `npm test` after fix (Linux) | `Test Files 2 passed (2)`, `Tests 15 passed (15)` | PASS | |
| B2w | `Backend tests (Windows)` CI job | Green in CI run #7 (windows-latest, Node 24, all 15 tests) | PASS | Windows proof of the B2 fix: https://github.com/tangofoxtrotyankee/Lapper/actions/runs/33771872834 |
| B3 | `format:check` from LF checkout | `All matched files use Prettier code style!` | PASS | `.gitattributes` now forces LF on Windows checkouts; existing clones need the re-checkout step in the guide |
| A1 | Dependency review on PR run | pending | | PR #1 was merged before the fixes landed; verified on the follow-up fixes PR after Sam enables the Dependency graph repo setting |
| — | Full push CI run | Run #7: success — Backend, Backend tests (Windows), Contracts, Desktop, Secret scanning all green | PASS | |

---

## Verdict — Phase 0 acceptance criteria

| # | Criterion | Pass/Fail |
|---|---|---|
| 1 | Clean clone can build Windows solution | PASS (Session 1: A2, C1) |
| 2 | Backend starts locally and `/health/live` returns 200 | PASS (Session 1: B5) |
| 3 | All automated tests pass | PASS (Session 2: B2, B2w) |
| 4 | No production secrets required for basic build | PASS (Session 1: A3, B5) |
| 5 | CI runs desktop build, backend typecheck/tests, schema validation | pending Session 2 (A1) |
| 6 | Orientation schema validates known good/bad fixtures | PASS (Session 1: B4, C2) |

**Phase 0 accepted — Phase 1 may begin:** pending — blocked only on A1
(Dependency review green on the fixes PR once the repository's Dependency
graph setting is enabled). B2/B2w and B3 are resolved and verified above.
