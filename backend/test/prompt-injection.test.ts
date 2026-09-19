import { describe, expect, it } from 'vitest';
import {
  assembleActionCall,
  assembleOrientCall,
  orientationSystemPrompt,
} from '../src/ai/prompt.js';
import type { ActionRequest, OrientRequest } from '../src/contracts/types.js';
import { INJECTION_FIXTURES } from './fixtures/injection.js';

function orientRequestWith(text: string): OrientRequest {
  return {
    requestId: '11111111-2222-3333-4444-555555555555',
    application: { processName: 'msedge.exe', windowTitle: null, category: 'browser' },
    context: {
      selectedText: null,
      blocks: [{ id: 'b1', role: 'document', text }],
      ocrText: null,
      imageIncluded: false,
    },
    client: { version: '0.1.0', capabilities: ['uia'] },
  };
}

describe('prompt boundary under injection fixtures', () => {
  for (const fixture of INJECTION_FIXTURES) {
    it(`keeps instructions untouched for fixture: ${fixture.name}`, () => {
      const call = assembleOrientCall(orientRequestWith(fixture.text));

      // Trusted instructions are exactly the system prompt — hostile screen
      // content can never reach them.
      expect(call.instructions).toBe(orientationSystemPrompt());
      expect(call.instructions).not.toContain(fixture.text);

      // The hostile text rides only inside the JSON-encoded screen_context.
      expect(call.userInput).toContain('SCREEN_CONTEXT (untrusted data, JSON):');
      expect(call.userInput).toContain(JSON.stringify(fixture.text).slice(1, -1));
    });
  }

  it('JSON-encodes screen content so it cannot break out of the payload', () => {
    const hostile = INJECTION_FIXTURES.find((f) => f.name === 'json-breakout')!;
    const call = assembleOrientCall(orientRequestWith(hostile.text));
    const jsonStart = call.userInput.indexOf('\n', call.userInput.indexOf('SCREEN_CONTEXT'));
    const payload = call.userInput.slice(jsonStart + 1);
    // The payload parses back as exactly one JSON object with our shape.
    const parsed = JSON.parse(payload) as { blocks: { text: string }[] };
    expect(parsed.blocks[0]!.text).toBe(hostile.text);
  });

  it('keeps action task instructions independent of screen content', () => {
    const hostile = INJECTION_FIXTURES[0]!;
    const request: ActionRequest = {
      ...orientRequestWith(hostile.text),
      action: { type: 'ask_question', question: 'What is the deadline?' },
    };
    const call = assembleActionCall(request);
    expect(call.instructions).not.toContain(hostile.text);
    const taskLine = call.userInput.split('\n')[0]!;
    expect(taskLine.startsWith('TASK: ')).toBe(true);
    expect(taskLine).not.toContain(hostile.text);
  });
});
