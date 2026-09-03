# Phase 1 Testing Guide — Windows Shell

How to verify the Phase 1 acceptance criteria (`phases/01-windows-shell.md`).
Record results in `docs/test-logs/phase-1.md`, one row per step, copying
exact output/behavior — never a paraphrase.

Phase 1 ships the shell only: tray icon, floating pill, global shortcut,
context card with placeholder states, settings. **No screen capture and no
AI** — seeing either would be a failure to record.

## Tier A — CI verification (~5 min, no installs)

**P1-A1.** GitHub → Actions → latest CI run on the branch: all jobs green,
including *Desktop (Windows solution build + tests)* which now builds the
full shell and runs two test suites — expected `Passed: 13` (contracts) and
`Passed: 25` (shell core: shortcut parsing, pill placement, settings).

## Tier B — automated tests on any machine (~5 min)

From the repo root (with the .NET 10 SDK):

```
dotnet test desktop/Lapper.Shell.Core.Tests
```

- Expected: `Passed! - Failed: 0, Passed: 25`.

## Tier C — manual Windows verification (~20 min)

Prereqs as Phase 0 (Visual Studio 2026 + WinUI workload). Pull the branch,
open `desktop/Lapper.slnx`, set `Lapper.Shell` / `x64` as startup, run.

**P1-C1. Starts without admin rights.**
Run from VS as a normal user (no elevation prompt). Expected: app starts;
a **Lapper tray icon** appears; the **floating pill** appears bottom-right.
No main window opens.

**P1-C2. Tray menu controls everything (no pill needed).**
Right-click the tray icon. Expected menu: Open Lapper, Show floating pill
(ticked), Settings, Exit Lapper. Untick "Show floating pill" → pill
disappears. Left-click the tray icon → the context card opens. This proves
the app is fully controllable without the pill.

**P1-C3. Global shortcut from another app.**
Give focus to any other app (e.g. Notepad). Press **Ctrl+Alt+L**.
Expected: the Lapper card appears in the bottom-right. Press Esc → hides.

**P1-C4. Pill behavior: focus, drag, persistence.**
- Re-enable the pill from the tray menu.
- Click into Notepad and type; then click the pill once. Expected: the card
  opens and — critically — while hovering/clicking the pill itself, Notepad
  **keeps keyboard focus** until the card opens (the pill never becomes the
  focused window).
- Drag the pill around. Expected: it moves smoothly and cannot leave the
  visible screen area.
- Drag it somewhere distinctive, Exit Lapper via the tray, run again.
  Expected: pill reappears **where you left it**.

**P1-C5. Context card states.**
Open the card, click **Expand**. Expected: the card grows and a "Preview
states" row appears. Click Loading / Error / Success. Expected: a spinner
with "Reading this screen…", an error panel, and the Phase 1 preview text
respectively. Collapse works.

**P1-C6. Settings.**
Tray → Settings. Expected: shortcut box showing `Ctrl+Alt+L`, a pill
toggle, and **Start Lapper with Windows** (off by default — record its
initial state; that default is an acceptance-relevant detail).
- Enter an invalid shortcut (e.g. `L`) → Save. Expected: validation message,
  nothing saved.
- Enter `Ctrl+Alt+K` → Save. Expected: "Saved."; the new shortcut works
  from another app and the old one no longer does. Set it back if you like.
- Toggle Start with Windows on → Windows may show a consent flow; check
  Task Manager → Startup apps shows Lapper. Toggle off afterwards.

**P1-C7. Single instance.**
With Lapper running, launch it again (Start menu or VS Ctrl+F5 a second
copy). Expected: **no second tray icon or pill**; the existing instance's
card comes to the front instead.

**P1-C8. No capture (scope check).**
Nothing in the app should read, display or transmit screen content. The
card only ever shows the fixed preview text. Any real screen content
appearing anywhere is a scope violation — record it as FAIL.

## If something fails

Same procedure as Phase 0: capture the exact step ID, what you saw vs
expected, and any error text; log it in `docs/test-logs/phase-1.md`; paste
the same into a Claude Code session on this repo.

## Mapping to acceptance criteria

| Acceptance criterion | Steps |
|---|---|
| app starts without admin rights | P1-C1 |
| shortcut opens Lapper from another app | P1-C3, P1-C6 |
| floating pill never steals focus unnecessarily | P1-C4 |
| pill position survives restart | P1-C4 |
| app can be fully controlled without floating pill | P1-C2, P1-C3 |
| no screen content is captured in this phase | P1-C8 |
