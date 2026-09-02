# Phase 5 - Accounts and Data

## Goal
Add secure identity, tenant isolation, settings sync and usage metadata.

## Tasks
- Auth0 native PKCE login
- refresh token secure storage
- device registration/revocation
- Neon database
- Drizzle migrations
- users/orgs/memberships
- settings
- usage metadata
- AI run metadata
- RLS
- runtime/migrator DB roles

## Acceptance criteria
- no client secret embedded in app
- expired access token refreshes securely
- revoked device loses access
- cross-tenant RLS tests pass
- DB contains no screen content after end-to-end tests
