# Phase 1 - Windows Shell

## Goal
Lapper exists as a polished local Windows utility shell.

## Tasks
- system tray lifecycle
- floating always-on-top pill
- drag/reposition pill
- user toggle to hide pill
- configurable global shortcut
- compact expandable context card
- loading/error/success states
- local settings persistence
- single-instance enforcement
- startup option, default off during alpha

## Acceptance criteria
- app starts without admin rights
- shortcut opens Lapper from another app
- floating pill never steals focus unnecessarily
- pill position survives restart
- app can be fully controlled without floating pill
- no screen content is captured in this phase
