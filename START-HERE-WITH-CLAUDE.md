# Start Here With Claude Code

Open the root of the Lapper repository in Claude Code.

Paste the following as the first instruction:

> Read `CLAUDE.md`, `README.md`, `docs/01-product-brief.md`, `docs/02-architecture.md`, `docs/03-security-threat-model.md` and `phases/00-foundation.md` in full. Do not write code until you have read them. Then inspect the repository and produce a Phase 0 implementation checklist mapped exactly to the Phase 0 acceptance criteria. Identify any missing prerequisites or conflicts. Do not redesign the architecture. Once the checklist is complete, begin Phase 0 in small commits/tasks, running the build and tests after each material step. Stop at the end of Phase 0 and give me an acceptance-criteria report before starting Phase 1.

## After Phase 0

Use:

> Read the next phase file in full. Compare the current repository against its tasks and acceptance criteria. Produce a short implementation checklist, then implement only that phase. Preserve all security and privacy rules in CLAUDE.md. Run tests continuously. At completion, produce an acceptance-criteria report and do not start the next phase automatically.

## When Claude proposes an architecture change

Tell it:

> Do not implement that change. Create an ADR under `docs/adr/` using the policy in CLAUDE.md, including the security/privacy impact and migration cost. Keep the current architecture until I approve the ADR.
