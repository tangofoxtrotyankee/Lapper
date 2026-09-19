import { Writable } from 'node:stream';
import { pino } from 'pino';
import { describe, expect, it } from 'vitest';
import { buildApp } from '../src/app.js';
import { loadConfig } from '../src/config.js';
import { MockGateway, validActionRequest, validOrientRequest } from './helpers/mockGateway.js';
import { INJECTION_FIXTURES } from './fixtures/injection.js';

const CANARY = 'CANARY-9f31-the-user-bank-balance-is-42';

describe('operational logs contain no captured content', () => {
  it('never logs screen text, prompts or model output', async () => {
    const lines: string[] = [];
    const sink = new Writable({
      write(chunk: Buffer, _encoding, callback) {
        lines.push(chunk.toString('utf8'));
        callback();
      },
    });
    const logger = pino({ level: 'trace' }, sink);

    const modelReply = `MODEL-OUTPUT-${CANARY}`;
    const gateway = new MockGateway(() => [
      { type: 'delta', text: modelReply },
      { type: 'complete', text: modelReply }, // invalid JSON on purpose
    ]);
    const config = loadConfig({});
    const app = buildApp({
      config: { ...config, openai: { ...config.openai, apiKey: 'k' } },
      gateway,
      loggerInstance: logger,
    });

    const payload = validOrientRequest();
    (payload['context'] as { blocks: { id: string; role: string; text: string }[] }).blocks = [
      { id: 'b1', role: 'body', text: CANARY },
      { id: 'b2', role: 'body', text: INJECTION_FIXTURES[0]!.text },
    ];

    await app.inject({
      method: 'POST',
      url: '/v1/context/orient',
      headers: { 'content-type': 'application/json' },
      payload,
    });
    await app.close();

    const logged = lines.join('');
    expect(logged.length).toBeGreaterThan(0); // metadata lines exist
    expect(logged).not.toContain(CANARY);
    expect(logged).not.toContain('IGNORE ALL PREVIOUS INSTRUCTIONS');
  });

  it('canary matrix: every content-bearing request field stays out of the logs', async () => {
    const canaries = {
      block: 'CANARY-BLOCK-a1f0',
      selected: 'CANARY-SELECTED-b2e1',
      ocr: 'CANARY-OCR-c3d2',
      title: 'CANARY-TITLE-d4c3',
      question: 'CANARY-QUESTION-e5b4',
      delta: 'CANARY-DELTA-f6a5',
      internal: 'CANARY-INTERNAL-0797',
    };
    const lines: string[] = [];
    const sink = new Writable({
      write(chunk: Buffer, _encoding, callback) {
        lines.push(chunk.toString('utf8'));
        callback();
      },
    });
    const gateway = new MockGateway(() => [
      { type: 'delta', text: canaries.delta },
      { type: 'complete', text: canaries.delta },
    ]);
    const config = loadConfig({});
    const app = buildApp({
      config: { ...config, openai: { ...config.openai, apiKey: 'k' } },
      gateway,
      loggerInstance: pino({ level: 'trace' }, sink),
    });

    const orient = validOrientRequest();
    (orient['context'] as Record<string, unknown>)['blocks'] = [
      { id: 'b1', role: 'body', text: canaries.block },
    ];
    (orient['context'] as Record<string, unknown>)['selectedText'] = canaries.selected;
    (orient['context'] as Record<string, unknown>)['ocrText'] = canaries.ocr;
    (orient['application'] as Record<string, unknown>)['windowTitle'] = canaries.title;
    await app.inject({
      method: 'POST',
      url: '/v1/context/orient',
      headers: { 'content-type': 'application/json' },
      payload: orient,
    });

    // Action route with a content-bearing question.
    await app.inject({
      method: 'POST',
      url: '/v1/context/action',
      headers: { 'content-type': 'application/json' },
      payload: validActionRequest('ask_question', canaries.question),
    });

    // Unexpected internal error whose message carries content: the route
    // must emit the fixed INTERNAL_ERROR envelope and log metadata only.
    const throwingGateway = new MockGateway(() => new Error(`exploded: ${canaries.internal}`));
    const app2 = buildApp({
      config: { ...config, openai: { ...config.openai, apiKey: 'k' } },
      gateway: throwingGateway,
      loggerInstance: pino({ level: 'trace' }, sink),
    });
    const errResponse = await app2.inject({
      method: 'POST',
      url: '/v1/context/orient',
      headers: { 'content-type': 'application/json' },
      payload: validOrientRequest(),
    });
    await app.close();
    await app2.close();

    expect(errResponse.body).not.toContain(canaries.internal);
    const logged = lines.join('');
    expect(logged.length).toBeGreaterThan(0);
    for (const canary of Object.values(canaries)) {
      expect(logged).not.toContain(canary);
    }
  });
});
