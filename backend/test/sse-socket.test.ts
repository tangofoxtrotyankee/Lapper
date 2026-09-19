import { setTimeout as delay } from 'node:timers/promises';
import { describe, expect, it } from 'vitest';
import { buildApp } from '../src/app.js';
import { loadConfig } from '../src/config.js';
import type { GatewayCall, GatewayEvent, ModelGateway } from '../src/gateway/types.js';
import { validOrientRequest } from './helpers/mockGateway.js';

/**
 * Real-socket SSE lifecycle tests. app.inject() never exercises the actual
 * IncomingMessage/ServerResponse close semantics, which is exactly how the
 * "request 'close' fires on body completion and aborts every model call"
 * bug slipped past the suite — these tests listen on a real port.
 */

class SlowGateway implements ModelGateway {
  readonly calls: GatewayCall[] = [];
  abortedAtTick: number | null = null;

  constructor(private readonly ticks: number) {}

  async *stream(call: GatewayCall): AsyncGenerator<GatewayEvent, void, void> {
    this.calls.push(call);
    for (let i = 0; i < this.ticks; i++) {
      await delay(50);
      if (call.signal?.aborted) {
        this.abortedAtTick = i;
        const reason = new Error('aborted');
        reason.name = 'AbortError';
        throw reason;
      }
      yield { type: 'delta', text: `d${i}` };
    }
    yield { type: 'complete', text: 'done' };
  }
}

function makeApp(gateway: ModelGateway) {
  const config = loadConfig({});
  return buildApp({
    config: { ...config, openai: { ...config.openai, apiKey: 'k' } },
    gateway,
  });
}

describe('SSE over a real socket', () => {
  it('a connected client receives deltas and completion (no spurious abort)', async () => {
    const gateway = new SlowGateway(3);
    const app = makeApp(gateway);
    await app.listen({ port: 0, host: '127.0.0.1' });
    const { port } = app.server.address() as { port: number };

    const response = await fetch(`http://127.0.0.1:${port}/v1/context/orient`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(validOrientRequest()),
    });
    expect(response.status).toBe(200);
    const body = await response.text();
    await app.close();

    expect(gateway.abortedAtTick).toBeNull();
    expect(body).toContain('event: accepted');
    expect(body).toContain('event: orientation.delta');
    expect(body.match(/event: orientation\.delta/g)!.length).toBe(3);
    // complete text 'done' is not valid orientation JSON → error event, but
    // the stream must terminate properly with a usage frame either way.
    expect(body).toContain('event: usage');
  });

  it('a client disconnect mid-stream aborts the gateway call', async () => {
    const gateway = new SlowGateway(50); // ~2.5s if never aborted
    const app = makeApp(gateway);
    await app.listen({ port: 0, host: '127.0.0.1' });
    const { port } = app.server.address() as { port: number };

    const controller = new AbortController();
    const request = fetch(`http://127.0.0.1:${port}/v1/context/orient`, {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify(validOrientRequest()),
      signal: controller.signal,
    }).catch(() => undefined);

    await delay(180); // let a few deltas flow, then drop the connection
    controller.abort();
    await request;

    // The server must notice the disconnect and abort the model call.
    let waited = 0;
    while (gateway.abortedAtTick === null && waited < 2000) {
      await delay(50);
      waited += 50;
    }
    await app.close();
    expect(gateway.abortedAtTick).not.toBeNull();
  });
});
