# Lapper Contracts

Single source of truth for API contracts shared by the backend and the
Windows desktop client.

- `orientation.schema.json` — JSON Schema (2020-12) for the orientation
  result returned by `POST /v1/context/orient`. Do not edit without updating
  both stacks and the fixtures.
- `openapi.yaml` — OpenAPI 3.1 document. Phase 0 covers health endpoints;
  `/v1` endpoints are added phase by phase per `docs/04-api-contract.md`.
- `fixtures/valid/` — orientation results that MUST validate.
- `fixtures/invalid/` — orientation results that MUST fail validation
  (missing fields, injection-style extra properties, disallowed action types,
  over-limit sizes).

Consumers:

- Backend: `backend/src/contracts/orientation.ts` compiles the schema with
  Ajv and validates fixtures in `backend/test/orientation-schema.test.ts`.
- Desktop: `desktop/Lapper.Contracts` embeds the schema and fixtures as
  resources; `Lapper.Contracts.Tests` runs the same fixture assertions with
  JsonSchema.Net.

Fixtures are synthetic. Never place real captured screen content, personal
data or secrets in fixtures (CLAUDE.md privacy rules).
