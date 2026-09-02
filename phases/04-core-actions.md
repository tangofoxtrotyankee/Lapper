# Phase 4 - Core Actions

## Goal
Move from explanation to useful action while retaining strict safety boundaries.

## Tasks
- action policy engine
- copy text
- read aloud using local Windows TTS
- draft text
- extract facts
- ask follow-up question
- share text via safe Windows mechanism if practical
- action result UX

## Acceptance criteria
- only allowlisted actions are accepted
- model-proposed unknown action is rejected
- local actions do not execute arbitrary commands
- TTS can read orientation and full generated result
- copy/draft actions require no cloud persistence
