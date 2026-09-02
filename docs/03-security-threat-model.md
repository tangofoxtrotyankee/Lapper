# Security and Threat Model

## Security objective
Capture as little as possible, store even less, and never let the AI directly control the computer.

## Assets
- screen content
- selected text
- clipboard content
- OCR output
- screenshots
- authentication tokens
- account metadata
- billing metadata
- Laps/configuration
- AI provider credentials
- signing credentials

## Main threat classes

### Accidental capture of sensitive information
Examples:
- passwords
- banking data
- password managers
- API keys
- private messages

Controls:
- explicit user trigger only
- active-window scope
- application exclusion list
- block password controls
- sensitive-pattern scan
- no automatic screen history

### Prompt injection from screen content
Threat:
A webpage/document tells the model to ignore instructions or take actions.

Controls:
- screen content is untrusted data
- clear prompt boundary
- no arbitrary OS tools
- strict structured output
- action allowlist
- local action policy
- confirmations for state changes

### Tenant data leakage
Controls:
- Auth0 subject validation
- organisation membership checks
- Postgres RLS
- runtime DB role cannot bypass RLS
- integration tests for cross-tenant access

### API credential theft
Controls:
- provider secrets backend-only
- no desktop client secret
- PKCE
- short-lived access tokens
- rotating refresh tokens
- Windows secure credential storage

### Screenshot retention/log leakage
Controls:
- screenshots in memory only
- no request body logging
- no Sentry screenshots/session replay
- no model response text logging
- scrub error telemetry

### Abuse/cost exhaustion
Controls:
- per-user/account quotas
- request size bounds
- token output ceiling
- timeouts
- image dimension/pixel limits
- usage anomaly monitoring

### Supply-chain compromise
Controls:
- signed MSIX
- protected branches
- dependency scanning
- secret scanning
- CodeQL
- pinned critical dependencies where practical
- SBOM
- controlled signing identity

## Action risk classes

### Class A: local/read-only
Can execute without separate confirmation after user selects action:
- copy generated text
- read aloud
- extract facts
- show explanation

### Class B: state-changing but reversible
Future. Require explicit confirmation:
- create reminder
- create calendar event
- save note

### Class C: external communication/destructive/financial
Not permitted in MVP:
- send email/message
- delete/modify files
- submit forms
- purchase/pay
- execute scripts
- autonomous browser clicks

## Logging policy
Allowed:
- request ID
- pseudonymous user/org ID
- app version
- acquisition method flags
- model name
- route
- token counts
- latency
- status/error code

Prohibited:
- screenshots
- UI text
- OCR text
- selected text
- clipboard
- prompts
- user questions
- model answers
- auth tokens
- audio

## Required security tests before beta
- prompt injection fixture suite
- RLS tenant escape tests
- oversized image rejection
- invalid MIME/magic byte rejection
- excluded-app enforcement
- password-field enforcement
- secret-pattern redaction/gating
- auth token expiry/refresh behavior
- model action allowlist rejection
- log inspection test ensuring content absence
