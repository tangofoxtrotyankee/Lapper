# Phase 3 - AI Orientation

## Goal
One trigger produces a near-immediate useful orientation in the overlay.

## Tasks
- implement backend ModelGateway
- OpenAI Responses adapter
- model routing config
- `store:false`
- orientation structured output
- SSE streaming
- API cancellation/timeouts
- desktop API client
- source evidence mapping
- request metadata logging only
- token/cost accounting

## Acceptance criteria
- pressing Lapper on a supported screen shows streamed orientation
- no OpenAI key exists in desktop package
- malformed model result is rejected safely
- timeout/cancel leaves app recoverable
- prompt injection fixtures do not alter Lapper instructions
- model responses are not written to operational logs
