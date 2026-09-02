# Phase 6 - Security Hardening

## Goal
Prepare for real external alpha users.

## Tasks
- threat model review
- app exclusion defaults
- secret pattern gate
- image MIME/magic-byte validation
- image dimension/pixel limits
- rate limits and account spend ceilings
- metadata log redaction tests
- Sentry safe configuration
- dependency/CodeQL/security CI
- signed MSIX pipeline
- privacy settings UX
- account export/delete scaffolding

## Acceptance criteria
- security test suite passes
- no high severity known dependency vulnerabilities without documented exception
- package is code signed
- logs inspected under failure conditions and contain no screen content
- prompt injection red-team fixture suite passes defined threshold
