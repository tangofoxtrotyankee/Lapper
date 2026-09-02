# Definition of Private Beta

Lapper is ready for private beta only when all of the following are true.

## Functional
- signed installer works on a clean supported Windows machine
- global shortcut works reliably
- floating pill can be disabled
- Edge/Chrome/Outlook/Word/Notepad context acquisition works to agreed test threshold
- orientation streams successfully
- copy/read/draft/extract/ask actions work
- authentication/device revocation works
- updater has a safe signed path

## Performance
- local trigger feedback under 100 ms typical
- useful request dispatch under 700 ms typical on supported UIA screens
- median first useful streamed orientation targeted below 2 seconds on test broadband
- slow path is visibly represented, never silent

## Security/privacy
- threat model reviewed
- content-free logging verified
- RLS escape tests pass
- prompt injection test threshold passes
- password/app exclusions pass
- package signed
- secrets scan clean
- provider data-control configuration documented
- DPIA completed/reviewed
- incident response owner/process defined

## AI quality
- minimum 30 eval fixtures
- deadlines/amounts factual extraction reaches agreed threshold
- unsupported claim rate below agreed threshold
- regression gate runs on proposed model/prompt changes

## Product
- action-take rate measurable
- invocation frequency measurable
- feedback mechanism available
- AI spend/request measurable
- user can delete cloud account metadata
