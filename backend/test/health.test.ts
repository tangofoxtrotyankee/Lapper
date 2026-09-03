import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import type { FastifyInstance } from 'fastify';
import { buildApp } from '../src/app.js';

describe('health routes', () => {
  let app: FastifyInstance;

  beforeAll(async () => {
    app = buildApp();
    await app.ready();
  });

  afterAll(async () => {
    await app.close();
  });

  it('GET /health/live returns 200 with status live', async () => {
    const response = await app.inject({ method: 'GET', url: '/health/live' });
    expect(response.statusCode).toBe(200);
    expect(response.json()).toEqual({ status: 'live' });
  });

  it('GET /health/ready returns 200 with status ready', async () => {
    const response = await app.inject({ method: 'GET', url: '/health/ready' });
    expect(response.statusCode).toBe(200);
    expect(response.json()).toEqual({ status: 'ready' });
  });

  it('rejects request bodies above the configured bound', async () => {
    const oversized = 'x'.repeat(300 * 1024);
    const response = await app.inject({
      method: 'POST',
      url: '/health/live',
      payload: oversized,
      headers: { 'content-type': 'text/plain' },
    });
    // 404 (no POST route) would also be acceptable, but Fastify checks the
    // body limit first and returns 413 for oversized payloads.
    expect(response.statusCode).toBe(413);
  });
});
