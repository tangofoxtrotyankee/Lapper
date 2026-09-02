# Phase 2 - Local Context Engine

## Goal
Extract useful active-window context locally with no cloud AI.

## Tasks
- foreground window/process identification
- exclusion policy framework
- UI Automation tree extraction
- focused/selected element detection
- block normalisation and deduplication
- relevance ranking
- local OCR fallback
- active-window screenshot fallback
- context preview developer panel, debug builds only
- in-memory screenshot handling

## Acceptance criteria
- extracts useful text from Edge, Chrome, Outlook, Word and Notepad test cases
- returns source block IDs
- excluded apps return blocked status before capture
- password fields are excluded
- screenshot fallback captures only active target window
- screenshots are never written to disk
- telemetry contains no captured text
