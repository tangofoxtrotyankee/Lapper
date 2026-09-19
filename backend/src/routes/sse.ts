import type { FastifyReply } from 'fastify';

/**
 * Minimal SSE writer over the raw response. The reply must be hijacked by
 * the caller so Fastify does not also try to send a response.
 */
export class SseWriter {
  private open = true;
  private readonly keepAlive: NodeJS.Timeout;

  constructor(private readonly reply: FastifyReply) {
    reply.raw.writeHead(200, {
      'content-type': 'text/event-stream',
      'cache-control': 'no-store, no-cache, no-transform',
      connection: 'keep-alive',
      'x-accel-buffering': 'no',
    });
    reply.raw.on('close', () => {
      this.open = false;
      clearInterval(this.keepAlive);
    });
    // Comment frames defeat intermediary buffering during long model calls.
    this.keepAlive = setInterval(() => {
      if (this.open) {
        this.reply.raw.write(': keep-alive\n\n');
      }
    }, 15_000);
    this.keepAlive.unref();
  }

  send(event: string, data: unknown): void {
    if (!this.open) {
      return;
    }
    this.reply.raw.write(`event: ${event}\ndata: ${JSON.stringify(data)}\n\n`);
  }

  end(): void {
    clearInterval(this.keepAlive);
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
