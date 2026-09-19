import { Writable } from 'node:stream';
import { pino } from 'pino';
import { describe, expect, it } from 'vitest';
import { buildApp } from '../src/app.js';
import { loadConfig } from '../src/config.js';
import { actionSystemPrompt, orientationSystemPrompt } from '../src/ai/prompt.js';
import {
  MockGateway,
  parseSseBody,
  validActionRequest,
  validOrientRequest,
} from './helpers/mockGateway.js';
import { INJECTION_FIXTURES } from './fixtures/injection.js';

/**
 * Route-level prompt-injection suite (threat model §injection, design
 * checklist assertions A1–A5): every fixture is POSTed through the real
 * routes and must stay contained in the untrusted data plane.
 */

function harness(script?: (call: { userInput: string }) => { type: string; text: string }[]) {
  const lines: string[] = [];
  const sink = new Writable({
    write(chunk: Buffer, _encoding, callback) {
      lines.push(chunk.toString('utf8'));
      callback();
    },
  });
  const gateway = new MockGateway(
    (call) =>
      (script?.(call) as never) ?? [
        { type: 'delta', text: 'ok' },
        { type: 'complete', text: 'ok' },
      ],
  );
  const config = loadConfig({});
  const app = buildApp({
    config: { ...config, openai: { ...config.openai, apiKey: 'k' } },
    gateway,
    loggerInstance: pino({ level: 'trace' }, sink),
  });
  return { app, gateway, lines };
}

describe('route-level injection containment (A1, A2, A5)', () => {
  for (const fixture of INJECTION_FIXTURES) {
    it(`contains fixture in the data plane: ${fixture.name}`, async () => {
      const { app, gateway, lines } = harness();
      const payload = validOrientRequest();

      if (fixture.name === 'windowtitle-injection') {
        // Hostile text arrives via the window title field instead of blocks.
        (payload['application'] as { windowTitle: string | null }).windowTitle = fixture.text;
      } else {
        (payload['context'] as { blocks: unknown[] }).blocks = [
          { id: 'b1', role: 'body', text: fixture.text },
        ];
      }

      const response = await app.inject({
        method: 'POST',
        url: '/v1/context/orient',
        headers: { 'content-type': 'application/json' },
        payload,
      });
      await app.close();

      expect(response.statusCode).toBe(200);
      const call = gateway.calls[0]!;

      // A1 — instructions byte-identical to the system prompt.
      expect(call.instructions).toBe(orientationSystemPrompt());
      expect(call.instructions).not.toContain(fixture.text);

      // A2 — the fixture rides only inside the JSON payload after the
      // untrusted-data marker, and that payload round-trips as data.
      const marker = 'SCREEN_CONTEXT (untrusted data, JSON):';
      expect(call.userInput).toContain(marker);
      const json = call.userInput.slice(call.userInput.indexOf(marker) + marker.length);
      const parsed = JSON.parse(json.trim()) as {
        application: { windowTitle: string | null };
        blocks: { text: string }[];
      };
      if (fixture.name === 'windowtitle-injection') {
        expect(parsed.application.windowTitle).toBe(fixture.text);
      } else {
        expect(parsed.blocks[0]!.text).toBe(fixture.text);
      }

      // A5 — operational logs never carry the fixture text.
      expect(lines.join('')).not.toContain(fixture.text.trim().slice(0, 40));
    });
  }

  it('question-injection: question stays in the user-question slot, task line unchanged', async () => {
    const fixture = INJECTION_FIXTURES.find((f) => f.name === 'question-injection')!;
    const { app, gateway, lines } = harness();
    const response = await app.inject({
      method: 'POST',
      url: '/v1/context/action',
      headers: { 'content-type': 'application/json' },
      payload: validActionRequest('ask_question', fixture.text),
    });
    await app.close();

    expect(response.statusCode).toBe(200);
    const call = gateway.calls[0]!;
    expect(call.instructions).toBe(actionSystemPrompt());
    expect(call.instructions).not.toContain(fixture.text);
    const taskLine = call.userInput.split('\n')[0]!;
    expect(taskLine.startsWith('TASK: ')).toBe(true);
    expect(taskLine).not.toContain(fixture.text);
    expect(call.userInput).toContain('USER_QUESTION (from the user, answer it):');
    expect(lines.join('')).not.toContain(fixture.text.slice(0, 40));
  });
});

describe('SSE anti-forgery (A3)', () => {
  it('model deltas echoing SSE framing cannot forge frames', async () => {
    const fixture = INJECTION_FIXTURES.find((f) => f.name === 'sse-frame-forgery')!;
    const { app } = harness(() => [
      { type: 'delta', text: fixture.text },
      { type: 'complete', text: 'not-json' },
    ]);
    const response = await app.inject({
      method: 'POST',
      url: '/v1/context/orient',
      headers: { 'content-type': 'application/json' },
      payload: validOrientRequest(),
    });
    await app.close();

    const frames = parseSseBody(response.body);
    const names = frames.map((f) => f.event);
    // Exactly the legitimate protocol frames — no forged 'result' carrying
    // the injected contentType, because deltas are JSON.stringify-escaped.
    expect(names).toEqual(['accepted', 'orientation.delta', 'error', 'usage']);
    const delta = frames[1]!.data as { text: string };
    expect(delta.text).toBe(fixture.text);
    const forged = frames.filter(
      (f) => f.event === 'result' || JSON.stringify(f.data).includes('"contentType":"forged"'),
    );
    expect(forged.filter((f) => f.event === 'result')).toHaveLength(0);
  });
});

describe('action escalation via model output (A4)', () => {
  it('model returning a disallowed suggested action type yields MODEL_OUTPUT_INVALID, no result frame', async () => {
    const orientation = {
      contentType: 'email',
      orientation: 'This is a renewal notice with a deadline.',
      summary: 'Renewal notice.',
      keyFacts: [],
      suggestedActions: [{ type: 'send_email', label: 'Send now', payloadHint: null }],
      warnings: [],
      uncertainty: 'low',
    };
    const { app } = harness(() => [{ type: 'complete', text: JSON.stringify(orientation) }]);
    const response = await app.inject({
      method: 'POST',
      url: '/v1/context/orient',
      headers: { 'content-type': 'application/json' },
      payload: validOrientRequest(),
    });
    await app.close();

    const frames = parseSseBody(response.body);
    expect(frames.some((f) => f.event === 'result')).toBe(false);
    const error = frames.find((f) => f.event === 'error');
    expect(error).toBeDefined();
    expect((error!.data as { code: string }).code).toBe('MODEL_OUTPUT_INVALID');
  });
});
