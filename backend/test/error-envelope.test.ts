import { Writable } from 'node:stream';
import { pino } from 'pino';
import { describe, expect, it } from 'vitest';
import { buildApp } from '../src/app.js';
import { loadConfig } from '../src/config.js';
import { logMeta } from '../src/logging/meta.js';
import { validOrientRequest } from './helpers/mockGateway.js';

const CANARY = 'CANARY-e77a-super-secret-screen-text';

function appWithSink() {
  const lines: string[] = [];
  const sink = new Writable({
    write(chunk: Buffer, _encoding, callback) {
      lines.push(chunk.toString('utf8'));
      callback();
    },
  });
  const logger = pino({ level: 'trace' }, sink);
  const app = buildApp({ config: loadConfig({}), loggerInstance: logger });
  return { app, lines };
}

describe('error responses never echo request content', () => {
  it('malformed JSON body → fixed 400 envelope, no body snippet anywhere', async () => {
    const { app, lines } = appWithSink();
    // Node's JSON.parse SyntaxError normally quotes the failing input —
    // exactly what the custom error handler must suppress.
    const response = await app.inject({
      method: 'POST',
      url: '/v1/context/orient',
      headers: { 'content-type': 'application/json' },
      payload: `{"broken": ${CANARY}`,
    });
    await app.close();

    expect(response.statusCode).toBe(400);
    const body = response.json<{ error: { code: string; message: string } }>();
    expect(body.error.code).toBe('INVALID_REQUEST');
    expect(body.error.message).toBe('The request body is not valid.');
    expect(response.body).not.toContain(CANARY);
    expect(lines.join('')).not.toContain(CANARY);
  });

  it('oversized body → fixed 413 envelope, no content echo', async () => {
    const { app, lines } = appWithSink();
    const payload = validOrientRequest();
    (payload['context'] as { selectedText?: string }).selectedText =
      CANARY + 'x'.repeat(600 * 1024);
    const response = await app.inject({
      method: 'POST',
      url: '/v1/context/orient',
      headers: { 'content-type': 'application/json' },
      payload: JSON.stringify(payload),
    });
    await app.close();

    expect(response.statusCode).toBe(413);
    expect(response.json<{ error: { code: string } }>().error.code).toBe('CONTEXT_TOO_LARGE');
    expect(response.body).not.toContain(CANARY);
    expect(lines.join('')).not.toContain(CANARY);
  });

  it('wrong content type → rejected without echoing the payload', async () => {
    const { app, lines } = appWithSink();
    // Fastify 5 leaves unparseable content types with an undefined body,
    // which schema validation rejects; either way the payload must never
    // appear in the response or logs.
    const response = await app.inject({
      method: 'POST',
      url: '/v1/context/orient',
      headers: { 'content-type': 'text/plain' },
      payload: CANARY,
    });
    await app.close();

    expect(response.statusCode).toBeGreaterThanOrEqual(400);
    expect(response.statusCode).toBeLessThan(500);
    expect(response.body).not.toContain(CANARY);
    expect(lines.join('')).not.toContain(CANARY);
  });
});

describe('logMeta whitelist', () => {
  it('drops unknown keys and over-long strings, keeps allowed metadata', () => {
    expect(
      logMeta({
        requestId: 'r-1',
        route: 'default',
        latencyMs: 12,
        body: CANARY,
        text: CANARY,
        requestIdLong: CANARY,
      }),
    ).toEqual({ requestId: 'r-1', route: 'default', latencyMs: 12 });
    expect(logMeta({ requestId: 'x'.repeat(65) })).toEqual({});
  });
});
