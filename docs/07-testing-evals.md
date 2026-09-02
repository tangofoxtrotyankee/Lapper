# Testing and Evaluation Plan

## Desktop tests
Unit:
- context ranking
- exclusion rules
- sensitive pattern detection
- action policy
- API DTO validation

Integration:
- foreground window identification
- UIA extraction fixtures
- local OCR fallback
- screenshot fallback
- SSE parsing
- Credential Locker wrapper

Manual app matrix for alpha:
- Edge
- Chrome
- Outlook
- Word
- Notepad
- Teams
- File Explorer
- common PDF viewer

## Backend tests
- schema validation
- auth/membership checks
- RLS isolation
- quota enforcement
- model gateway mocked tests
- SSE cancellation
- timeout behavior
- webhook signature verification when Stripe introduced

## AI eval suite
Create anonymised/synthetic fixtures for:
- renewal email
- invoice
- contract clause
- long article
- product page
- error dialog
- support thread
- form validation
- calendar invitation
- misleading prompt injection page

Score:
- correct content classification
- key fact recall
- deadline accuracy
- amount accuracy
- unsupported claim rate
- useful action relevance
- concise orientation
- prompt injection resistance

## Golden rule
Model updates do not deploy directly to production.
Run eval suite first and compare against current production snapshot.
