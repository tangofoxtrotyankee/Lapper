# Architecture

## Principle
Native Windows client, thin secure API, metadata-only cloud database, transient AI processing.

## Components

### Desktop client
`Lapper.Shell`
- WinUI application lifecycle
- system tray
- floating pill
- context card
- settings

`Lapper.Context.Windows`
- foreground window detection
- UI Automation extraction
- selection retrieval
- OCR
- screenshot fallback

`Lapper.Privacy`
- exclusion policy
- password field detection
- secret pattern checks
- screenshot cropping/downscaling
- sensitive context gate

`Lapper.Actions`
- local action dispatcher
- action allowlist
- confirmation policy
- clipboard/TTS/share actions

`Lapper.ApiClient`
- authentication
- REST/SSE calls
- retry/cancellation
- error mapping

`Lapper.Contracts`
- generated/shared DTOs

## Context pipeline

Trigger
-> foreground window
-> exclusion check
-> selection check
-> UIA extraction
-> relevance/ranking
-> OCR fallback if needed
-> screenshot fallback if needed
-> privacy filter
-> API request
-> streaming orientation
-> suggested action

## Backend modules

- auth
- context/orientation
- model gateway
- action schemas
- usage/entitlements
- laps
- billing later
- feedback
- telemetry

## AI boundary
The backend is the only component that calls AI providers.

`ModelGateway` interface:
- `orient(context, options)`
- `performAction(context, action, options)`
- `answer(context, question, options)`

Provider adapter initially:
- OpenAI Responses API

## Model router
Default rules:

Luna:
- clean selected text
- short deterministic extraction
- low-complexity orientation

Terra:
- default
- mixed structured text
- screenshot/visual understanding
- drafting
- normal explanation

Sol:
- only user-selected Deep mode
- genuinely complex legal/technical/reasoning tasks

Routing must be configuration-driven and observable.

## API streaming
Use Server-Sent Events for orientation/action responses.

Events:
- `accepted`
- `orientation.delta`
- `orientation.complete`
- `result`
- `usage`
- `error`

## Deployment
MVP:
- one Railway API service in EU region
- Neon Postgres in compatible EU region
- Auth0 tenant
- OpenAI API project with EU controls where available

Do not horizontally scale until metrics justify shared distributed rate limiting.

## Local storage
SQLite stores:
- user preferences
- pill position
- app exclusions
- shortcut settings
- cached Lap configuration

Credentials/tokens do not belong in SQLite.

## Cross-platform strategy
Windows-first native client.

If validated, macOS client should be native Swift/SwiftUI and share:
- API contract
- product behavior
- prompt/eval system
- model routing
- cloud account model

Do not force UI code reuse across platforms at the expense of OS integration quality.
