# Phase 0 Test Log

Fill this in while following `docs/phase-0-testing-guide.md`.

Recording rules:
- Copy **exact** output text into "Actual result" — never paraphrase errors.
- If a result deviates at all, also record tool versions in Notes.
- Screenshots are optional; if used, name the file after the step
  (e.g. `phase0-B5-health-live.png`) and reference it in Notes.
- Never record real screen content, personal data or secrets.

## Test session

| Field | Value |
|---|---|
| Date | |
| Tester | |
| Machine / OS | |
| Node version (`node --version`) | |
| .NET SDK version (`dotnet --version`) | |
| Commit tested (`git rev-parse --short HEAD`) | |

## Results

| Step | Check | Expected | Actual result | Pass/Fail | Notes |
|---|---|---|---|---|---|
| A1 | CI run green | success; 4 jobs green, dependency review skipped | | | |
| A2 | CI desktop job detail | Build succeeded; Failed: 0, Passed: 13 | | | |
| A3 | No secrets in repo / gitleaks clean | placeholders only; `no leaks found` | | | |
| B1 | Clean clone + `npm ci` | completes, 0 vulnerabilities | | | |
| B2 | `npm test` | Tests 15 passed (15) | | | |
| B3 | lint / typecheck / format:check | all clean | | | |
| B4 | `npm run validate:contracts` | 3 valid + 7 invalid fixtures ok; all passed | | | |
| B5 | Server + health endpoints, no `.env` | `{"status":"live"}` and `{"status":"ready"}`, HTTP 200 | | | |
| C1 | `dotnet build desktop/Lapper.slnx -p:Platform=x64` | Build succeeded, 0 errors | | | |
| C2 | `dotnet test desktop/Lapper.Contracts.Tests` | Failed: 0, Passed: 13 | | | |
| C3 | Launch Lapper.Shell (optional) | empty "Lapper" window, Phase 0 message only | | | |

## Verdict — Phase 0 acceptance criteria

| # | Criterion | Pass/Fail |
|---|---|---|
| 1 | Clean clone can build Windows solution | |
| 2 | Backend starts locally and `/health/live` returns 200 | |
| 3 | All automated tests pass | |
| 4 | No production secrets required for basic build | |
| 5 | CI runs desktop build, backend typecheck/tests, schema validation | |
| 6 | Orientation schema validates known good/bad fixtures | |

**Phase 0 accepted — Phase 1 may begin:** yes / no

Signed: ______________  Date: ______________
