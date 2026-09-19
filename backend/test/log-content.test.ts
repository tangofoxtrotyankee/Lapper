import { Writable } from 'node:stream';
import { pino } from 'pino';
import { describe, expect, it } from 'vitest';
import { buildApp } from '../src/app.js';
import { loadConfig } from '../src/config.js';
import { MockGateway, validOrientRequest } from './helpers/mockGateway.js';
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
});
