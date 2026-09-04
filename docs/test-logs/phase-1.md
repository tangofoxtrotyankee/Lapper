# Phase 1 Test Log

Follows `docs/phase-1-testing-guide.md`. Same recording rules as Phase 0:
exact output text, versions on any deviation, no screen content or secrets.

## Test session

| Field | Value |
|---|---|
| Date | |
| Tester | |
| Machine / OS | |
| .NET SDK version | |
| Visual Studio version | |
| Commit tested (`git rev-parse --short HEAD`) | |

## Results

| Step | Check | Actual result | Pass/Fail | Notes |
|---|---|---|---|---|
| P1-A1 | CI green incl. desktop build + 13 + 25 tests | | | |
| P1-B1 | `dotnet test desktop/Lapper.Shell.Core.Tests` → 25 passed | | | |
| P1-C1 | Starts without admin; tray icon + pill appear | | | |
| P1-C2 | Tray menu: open card, pill toggle, settings, exit | | | |
| P1-C3 | Ctrl+Alt+L opens card from another app; Esc hides | | | |
| P1-C4 | Pill: no focus steal, drag clamped, position survives restart | | | |
| P1-C5 | Card expand/collapse + loading/error/success states | | | |
| P1-C6 | Settings: validation, shortcut change works, startup default off | | | |
| P1-C7 | Second launch → no second instance, card fronted | | | |
| P1-C8 | No screen content anywhere | | | |

## Verdict — Phase 1 acceptance criteria

| # | Criterion | Pass/Fail |
|---|---|---|
| 1 | App starts without admin rights | |
| 2 | Shortcut opens Lapper from another app | |
| 3 | Floating pill never steals focus unnecessarily | |
| 4 | Pill position survives restart | |
| 5 | App can be fully controlled without floating pill | |
| 6 | No screen content is captured in this phase | |

**Phase 1 accepted — Phase 2 may begin:** yes / no

Signed: ______________  Date: ______________
