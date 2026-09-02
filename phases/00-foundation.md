# Phase 0 - Foundation

## Goal
Create a reproducible repo that builds an empty Windows client and backend without implementing screen intelligence yet.

## Tasks
- create solution/repo structure
- create WinUI 3 packaged desktop app
- create project boundaries from CLAUDE.md
- create Node/TypeScript/Fastify backend
- configure TypeScript strict mode
- add formatting/linting
- add test projects
- add OpenAPI/schema package
- wire orientation JSON schema into both stacks
- configure local environment templates
- add GitHub Actions build/test workflows
- add secret scanning/dependency scanning
- create architecture decision record folder

## Acceptance criteria
- clean clone can build Windows solution
- backend starts locally and `/health/live` returns 200
- all automated tests pass
- no production secrets required for basic build
- CI runs desktop build, backend typecheck/tests and schema validation
- orientation schema validates known good/bad fixtures

## Do not build yet
- OpenAI integration
- screen capture
- UI Automation
- auth
- database
