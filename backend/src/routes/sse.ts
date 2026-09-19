import type { FastifyReply } from 'fastify';

/**
 * Minimal SSE writer over the raw response. The reply must be hijacked by
 * the caller so Fastify does not also try to send a response.
 */
export class SseWriter {
  private open = true;

  constructor(private readonly reply: FastifyReply) {
    reply.raw.writeHead(200, {
      'content-type': 'text/event-stream',
      'cache-control': 'no-cache, no-transform',
      connection: 'keep-alive',
      'x-accel-buffering': 'no',
    });
    reply.raw.on('close', () => {
      this.open = false;
    });
  }

  send(event: string, data: unknown): void {
    if (!this.open) {
      return;
    }
    this.reply.raw.write(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`);
  }

  end(): void {
    if (this.open) {
      this.open = false;
      this.reply.raw.end();
    }
  }
}

export interface ErrorEnvelope {
  readonly error: {
    readonly code: string;
    readonly message: string;
    readonly requestId: string;
  };
}

export function errorEnvelope(code: string, message: string, requestId: string): ErrorEnvelope {
  return { error: { code, message, requestId } };
}
