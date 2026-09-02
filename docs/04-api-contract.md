# API Contract

Base URL: `/v1`

## POST /context/orient
Starts initial screen orientation.

Request shape:
```json
{
  "requestId": "uuid",
  "application": {
    "processName": "OUTLOOK.EXE",
    "windowTitle": "optional-redacted-or-local-only",
    "category": "email"
  },
  "context": {
    "selectedText": null,
    "blocks": [
      {
        "id": "b1",
        "role": "subject",
        "text": "Your subscription renewal"
      }
    ],
    "ocrText": null,
    "imageIncluded": false
  },
  "client": {
    "version": "0.1.0",
    "capabilities": ["uia", "local_ocr", "local_tts"]
  }
}
```

Response streamed as SSE.

Final result:
```json
{
  "contentType": "renewal_notice",
  "orientation": "This is a supplier renewal notice with a price increase.",
  "summary": "The monthly fee rises from £400 to £472 on 1 October.",
  "facts": [
    {
      "label": "New monthly fee",
      "value": "£472",
      "sourceIds": ["b2"],
      "uncertainty": "low"
    }
  ],
  "suggestedActions": [
    {
      "type": "draft_text",
      "label": "Draft a negotiation reply",
      "requiresConfirmation": false
    }
  ],
  "warnings": [],
  "needsMoreContext": false
}
```

## POST /context/action
Executes an AI transformation, not an OS action.

Inputs:
- request ID
- action type
- action arguments from approved schema
- context reference or freshly supplied transient context

MVP action types:
- draft_text
- extract_facts
- ask_question

## GET /laps
Returns current user's available Laps.

## POST /laps
Creates a Lap configuration.

## PUT /laps/{id}
Updates allowed configuration only.

## DELETE /laps/{id}
Deletes a Lap.

## POST /devices/register
Registers an installation ID and platform metadata.

## DELETE /devices/{id}
Revokes a device session.

## GET /usage
Returns entitlement/usage counters.

## POST /feedback
Metadata feedback by default. Attaching source content must be a separate explicit opt-in.

## Health
- `GET /health/live`
- `GET /health/ready`

## Standard error envelope
```json
{
  "error": {
    "code": "CONTEXT_TOO_LARGE",
    "message": "The selected content is too large to analyse in one request.",
    "requestId": "uuid"
  }
}
```

Never return stack traces to clients.
