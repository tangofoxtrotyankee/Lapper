# Phase 1 Implementation Checklist

Maps `phases/01-windows-shell.md` tasks and acceptance criteria to concrete
deliverables. No screen capture, no AI calls, no auth in this phase.

## Acceptance criteria mapping

| Acceptance criterion | Deliverable | Verified by |
|---|---|---|
| app starts without admin rights | standard MSIX user install; no elevated APIs used | manual guide P1-C |
| shortcut opens Lapper from another app | `HotkeyService` (RegisterHotKey + hidden message window), gesture configurable via settings | manual guide P1-D |
| floating pill never steals focus unnecessarily | pill window created with `WS_EX_NOACTIVATE` + non-activating drag | manual guide P1-E |
| pill position survives restart | position persisted in SQLite `SettingsStore` on drag end, restored on launch (clamped to visible area) | unit tests + manual P1-E |
| app can be fully controlled without floating pill | tray icon menu (open card, show/hide pill, startup toggle, exit) + global shortcut | manual guide P1-F |
| no screen content is captured in this phase | no capture/UIA/OCR code exists; `Lapper.Context.Windows` and `Lapper.Privacy` remain placeholders | code review + CI |

## Phase 1 tasks → components

| Task | Component |
|---|---|
| system tray lifecycle | `TrayIconService` (H.NotifyIcon.WinUI 2.4.1, pinned); close hides to tray, exit only via tray menu |
| floating always-on-top pill | `PillWindow` — frameless `AppWindow`, `IsAlwaysOnTop`, hidden from switchers |
| drag/reposition pill | pointer drag on pill content, `PillPlacement` clamping logic in `Lapper.Shell.Core` |
| user toggle to hide pill | tray menu + persisted `pill.visible` setting |
| configurable global shortcut | `ShortcutGesture` parse/format in Core + `HotkeyService` (WM_HOTKEY) in Shell |
| compact expandable context card | `ContextCardWindow` with explicit Loading / Error / Success placeholder states |
| loading/error/success states | visual states on the card (static placeholder content this phase) |
| local settings persistence | `SettingsStore` (Microsoft.Data.Sqlite 10.0.11, pinned) in `Lapper.Shell.Core`; db in app local data; no tokens/credentials stored |
| single-instance enforcement | custom `Program.Main` + `AppInstance.FindOrRegisterForKey` redirect |
| startup option, default off | `uap5:StartupTask` (`Enabled="false"`) + tray toggle via `StartupTask` API |

## New projects

- `Lapper.Shell.Core` (net10.0, cross-platform): pure logic — shortcut
  gesture parsing/formatting, pill placement clamping, SQLite settings
  store. Keeps Phase 1 logic unit-testable on any OS (this repo's backend
  CI already proved the value of cross-platform test coverage).
- `Lapper.Shell.Core.Tests` (net10.0, xUnit): runs locally and in CI.

These are implementation/test additions inside the `Lapper.Shell` boundary,
not changes to the locked architecture.

## Testing

- Unit (cross-platform): gesture parse/format round-trips and rejection
  cases; placement clamping against monitor rects; settings store defaults,
  round-trip, persistence across reopen.
- CI: solution build + all test projects on windows-latest; Core tests also
  run on ubuntu in the backend job lane.
- Manual: `docs/phase-1-testing-guide.md` with results recorded in
  `docs/test-logs/phase-1.md`.
