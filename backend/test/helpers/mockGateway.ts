import type { GatewayCall, GatewayEvent, ModelGateway } from '../../src/gateway/types.js';

export interface RecordedCall {
  readonly call: GatewayCall;
}

/** Scripted gateway for tests: replays events and records calls. */
export class MockGateway implements ModelGateway {
  readonly calls: GatewayCall[] = [];

  constructor(private readonly script: (call: GatewayCall) => GatewayEvent[] | Error) {}

  // eslint-disable-next-line @typescript-eslint/require-await
  async *stream(call: GatewayCall): AsyncGenerator<GatewayEvent, void, void> {
    this.calls.push(call);
    const events = this.script(call);
    if (events instanceof Error) {
      throw events;
    }
    for (const event of events) {
      yield event;
    }
  }
}

/** Splits an SSE response body into {event, data} frames. */
export function parseSseBody(body: string): { event: string; data: unknown }[] {
  return body
    .split('\n\n')
    .filter((frame) => frame.trim().length > 0)
    .map((frame) => {
      let event = 'message';
      let data = '';
      for (const line of frame.split('\n')) {
        if (line.startsWith('event: ')) {
          event = line.slice(7);
        } else if (line.startsWith('data: ')) {
          data = line.slice(6);
        }
      }
      return { event, data: data.length > 0 ? (JSON.parse(data) as unknown) : undefined };
    });
}

export function validOrientRequest(): Record<string, unknown> {
  return {
    requestId: '11111111-2222-3333-4444-555555555555',
    application: { processName: 'OUTLOOK.EXE', windowTitle: null, category: 'email' },
    context: {
      selectedText: null,
      blocks: [
        { id: 'b1', role: 'subject', text: 'Your subscription renewal' },
        { id: 'b2', role: 'body', text: 'The monthly fee rises from £400 to £472 on 1 October.' },
      ],
      ocrText: null,
      imageIncluded: false,
    },
    client: { version: '0.1.0', capabilities: ['uia', 'local_ocr', 'local_tts'] },
  };
}

export function validActionRequest(type: string, question?: string): Record<string, unknown> {
  const base = validOrientRequest();
  return {
    ...base,
    action: { type, question: question ?? null },
  };
}
