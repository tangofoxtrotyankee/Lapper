import { afterEach, describe, expect, it, vi } from 'vitest';
import { OpenAiResponsesGateway } from '../src/gateway/openaiResponses.js';
import { GatewayError, type GatewayEvent } from '../src/gateway/types.js';

function sseResponse(frames: string[]): Response {
  const encoder = new TextEncoder();
  const stream = new ReadableStream<Uint8Array>({
    start(controller) {
      for (const frame of frames) {
        controller.enqueue(encoder.encode(frame));
      }
      controller.close();
    },
  });
  return new Response(stream, {
    status: 200,
    headers: { 'content-type': 'text/event-stream' },
  });
}

const CONFIG = {
  apiKey: 'test-key',
  baseUrl: 'https://api.openai.example/v1',
  models: { cheap: 'a', default: 'b', deep: 'c' },
  requestTimeoutMs: 5000,
  maxOutputTokens: 512,
};

async function collect(gen: AsyncGenerator<GatewayEvent>): Promise<GatewayEvent[]> {
  const events: GatewayEvent[] = [];
  for await (const event of gen) {
    events.push(event);
  }
  return events;
}

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('OpenAiResponsesGateway', () => {
  it('sends store:false with structured output and maps stream events', async () => {
    let captured: Record<string, unknown> | undefined;
    vi.stubGlobal('fetch', (_url: string, init: RequestInit) => {
      captured = JSON.parse(init.body as string) as Record<string, unknown>;
      return Promise.resolve(
        sseResponse([
          'event: response.output_text.delta\ndata: {"type":"response.output_text.delta","delta":"{\\"a\\":"}\n\n',
          'event: response.output_text.delta\ndata: {"type":"response.output_text.delta","delta":"1}"}\n\n',
          'event: response.completed\ndata: {"type":"response.completed","response":{"usage":{"input_tokens":10,"output_tokens":4},"output":[{"type":"message","content":[{"type":"output_text","text":"{\\"a\\":1}"}]}]}}\n\n',
        ]),
      );
    });

    const gateway = new OpenAiResponsesGateway(CONFIG);
    const events = await collect(
      gateway.stream({
        model: 'b',
        instructions: 'sys',
        userInput: 'user',
        outputSchema: { type: 'object' },
        maxOutputTokens: 512,
        signal: new AbortController().signal,
      }),
    );

    expect(captured).toBeDefined();
    expect(captured!['store']).toBe(false);
    expect(captured!['stream']).toBe(true);
    expect(captured!['model']).toBe('b');
    const text = captured!['text'] as { format: { type: string; strict: boolean } };
    expect(text.format.type).toBe('json_schema');
    expect(text.format.strict).toBe(true);

    expect(events.filter((e) => e.type === 'delta')).toHaveLength(2);
    expect(events.find((e) => e.type === 'usage')).toMatchObject({
      usage: { inputTokens: 10, outputTokens: 4 },
    });
    expect(events.at(-1)).toMatchObject({ type: 'complete', text: '{"a":1}' });
  });

  it('throws MODEL_UNAVAILABLE without an API key', async () => {
    const gateway = new OpenAiResponsesGateway({ ...CONFIG, apiKey: undefined });
    await expect(
      collect(
        gateway.stream({
          model: 'b',
          instructions: 's',
          userInput: 'u',
          maxOutputTokens: 1,
          signal: new AbortController().signal,
        }),
      ),
    ).rejects.toThrowError(GatewayError);
  });

  it('maps provider HTTP errors without exposing the body', async () => {
    vi.stubGlobal('fetch', () =>
      Promise.resolve(new Response('secret provider detail', { status: 429 })),
    );
    const gateway = new OpenAiResponsesGateway(CONFIG);
    const error = await collect(
      gateway.stream({
        model: 'b',
        instructions: 's',
        userInput: 'u',
        maxOutputTokens: 1,
        signal: new AbortController().signal,
      }),
    ).catch((e: unknown) => e as GatewayError);
    expect(error).toBeInstanceOf(GatewayError);
    expect((error as GatewayError).code).toBe('MODEL_ERROR');
    expect((error as GatewayError).message).not.toContain('secret provider detail');
  });

  it('fails cleanly when the stream ends without completion', async () => {
    vi.stubGlobal('fetch', () =>
      Promise.resolve(
        sseResponse([
          'event: response.output_text.delta\ndata: {"type":"response.output_text.delta","delta":"hi"}\n\n',
        ]),
      ),
    );
    const gateway = new OpenAiResponsesGateway(CONFIG);
    const error = await collect(
      gateway.stream({
        model: 'b',
        instructions: 's',
        userInput: 'u',
        maxOutputTokens: 1,
        signal: new AbortController().signal,
      }),
    ).catch((e: unknown) => e as GatewayError);
    expect((error as GatewayError).code).toBe('MODEL_ERROR');
  });
});
