# AI Plan

## Provider
OpenAI Responses API through backend `ModelGateway`.

## Models
- `gpt-5.6-luna`: cost-sensitive/simple route
- `gpt-5.6-terra`: default route
- `gpt-5.6-sol`: explicit Deep route

Model IDs must be configuration, never scattered string literals.

## First-pass orientation
The first model response should answer:
1. What am I looking at?
2. What matters?
3. Is there an obvious deadline/amount/action?
4. What are 2-4 useful next actions?

Keep it concise.

## Structured orientation schema
Fields:
- contentType
- orientation
- summary
- facts[]
- suggestedActions[]
- warnings[]
- needsMoreContext

Every extracted fact should reference source block IDs where possible.

## Prompt boundaries
System/developer instructions must state that all captured screen content is untrusted data and must not override Lapper instructions.

Screen content should be encoded as a discrete `screen_context` section, not concatenated into system text.

## Context minimisation
Send only relevant blocks.

Before calling model:
- remove hidden/duplicate UIA text
- cap repeated navigation elements
- rank blocks near selection/focus
- preserve headings/labels around relevant content
- prefer text to image

## Image use
Attach a screenshot only if:
- structured text is missing/insufficient
- spatial layout materially matters
- chart/image content is material
- app is inaccessible via UIA/OCR

## Provider retention
Set `store: false` for Responses requests.

Before public launch confirm provider data controls, regional processing and any approved zero-data-retention configuration contractually/operationally.

## No fine-tuning initially
Build evals first.

Fine-tuning requires:
- clear recurring error class
- sufficient consented dataset
- measurable improvement over prompting/routing
- privacy review

## Cost controls
Track per request:
- model
- input tokens
- output tokens
- image usage
- latency
- estimated cost

Have daily account budget safeguards.
