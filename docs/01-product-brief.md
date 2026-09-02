# Product Brief

## Product
Lapper

## Category
Desktop AI screen understanding and action assistant.

## One-line proposition
Lapper understands what is on your screen, tells you what matters, and helps you do the next thing.

## Problem
Users repeatedly encounter information inside emails, websites, PDFs, software interfaces and documents that requires interpretation or action. Today they often copy content, open an AI tool, paste it, explain context, ask a question, then transfer the result back.

Lapper removes that context-switching loop.

## Core insight
The screen the user is already looking at should become the prompt.

## MVP user flow
1. User presses global shortcut or floating Lapper control.
2. Lapper identifies the active application/window.
3. Lapper extracts the minimum useful context locally.
4. Lapper sends transient context to the backend.
5. A short orientation streams into a compact overlay.
6. Lapper presents context-aware actions.
7. User chooses an action or asks a follow-up question.

## Examples

### Renewal email
"This is a supplier renewal notice. Your monthly fee increases from £400 to £472 on 1 October. Cancellation is required by 12 September."

Suggested actions:
- Draft negotiation reply
- Extract deadlines
- Copy summary
- Read aloud

### Error message
"The app cannot connect because its API token has expired. Re-authentication is the likely next step."

Suggested actions:
- Explain simply
- Copy error details
- Draft support message
- Ask Lapper

### Contract clause
"This clause allows termination after an uncured breach following 14 days' written notice. Outstanding payment obligations survive termination."

Suggested actions:
- Explain in plain English
- Extract obligations
- Highlight dates
- Ask a question

## MVP target users
- knowledge workers
- business owners and managers
- users who deal with long emails/documents
- users who value accessibility or cognitive assistance
- support/admin/operations teams

## Differentiator
Not "AI reads a screenshot".

Lapper:
- understands structured context where possible
- returns a useful orientation automatically
- proposes relevant actions
- minimizes context switching
- is user-triggered rather than continuously watching

## MVP success metrics
Primary:
- percentage of Lapper invocations followed by a suggested action

Secondary:
- weekly active users
- invocations per active user
- time to first useful sentence
- percentage resolved without opening a full AI chat
- user correction/downvote rate
- UIA vs OCR vs screenshot acquisition mix
- average AI cost per completed action

## Explicit non-goals for MVP
- autonomous computer use
- continuous screen monitoring
- email sending
- browser automation
- file deletion
- payments
- long-term cloud screen history
- RAG over everything a user has viewed
- replacing specialist screen readers such as Narrator/VoiceOver
