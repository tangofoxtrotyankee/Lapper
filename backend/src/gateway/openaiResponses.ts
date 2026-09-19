import type { OpenAiConfig } from '../config.js';
import { GatewayError, type GatewayEvent, type GatewayCall, type ModelGateway } from './types.js';

/**
 * OpenAI Responses API adapter using Node's built-in fetch — no SDK
 * dependency, full control of streaming, timeouts and what gets logged
 * (nothing content-bearing).
 *
 * Requests always set `store: false` (CLAUDE.md locked decision).
 */

interface SseFrame {
  readonly event: string;
  readonly data: string;
}

/** Incremental SSE parser; exported for unit tests. */
export function* parseSseChunk(buffer: { pending: string }, chunk: string): Generator<SseFrame> {
  buffer.pending += chunk;
  let separatorIndex;
  while ((separatorIndex = buffer.pending.indexOf('\n\n')) !== -1) {
    const rawFrame = buffer.pending.slice(0, separatorIndex);
    buffer.pending = buffer.pending.slice(separatorIndex + 2);
    let event = 'message';
    const dataLines: string[] = [];
    for (const line of rawFrame.split('\n')) {
      if (line.startsWith('event:')) {
        event = line.slice(6).trim();
      } else if (line.startsWith('data:')) {
        dataLines.push(line.slice(5).trimStart());
      }
    }
    if (dataLines.length > 0) {
      yield { event, data: dataLines.join('\n') };
    }
  }
}

export class OpenAiResponsesGateway implements ModelGateway {
  constructor(private readonly config: OpenAiConfig) {}

  async *stream(call: GatewayCall): AsyncGenerator<GatewayEvent, void, void> {
    if (this.config.apiKey === undefined) {
      throw new GatewayError('MODEL_UNAVAILABLE', 'No model provider is configured.');
    }

    const timeoutSignal = AbortSignal.timeout(this.config.requestTimeoutMs);
    const signal = AbortSignal.any([call.signal, timeoutSignal]);

    const body: Record<string, unknown> = {
      model: call.model,
      instructions: call.instructions,
      input: [
        {
          role: 'user',
          content: [{ type: 'input_text', text: call.userInput }],
        },
      ],
      stream: true,
      store: false,
      max_output_tokens: call.maxOutputTokens,
    };
    if (call.outputSchema !== undefined) {
      body['text'] = {
        format: {
          type: 'json_schema',
          name: 'orientation_result',
          strict: true,
          schema: call.outputSchema,
        },
      };
    }

    let response: Response;
    try {
      response = await fetch(`${this.config.baseUrl}/responses`, {
        method: 'POST',
        headers: {
          authorization: `Bearer ${this.config.apiKey}`,
          'content-type': 'application/json',
        },
        body: JSON.stringify(body),
        signal,
      });
    } catch (error) {
      throw this.mapAbort(error, timeoutSignal, call.signal);
    }

    if (!response.ok || response.body === null) {
      // Drain without logging the body: provider errors may echo input.
      await response.body?.cancel().catch(() => undefined);
      throw new GatewayError('MODEL_ERROR', `Provider returned HTTP ${response.status}.`);
    }

    const decoder = new TextDecoder();
    const buffer = { pending: '' };
    const reader = (response.body as ReadableStream<Uint8Array>).getReader();
    let completedText: string | undefined;

    try {
      for (;;) {
        let read: Awaited<ReturnType<typeof reader.read>>;
        try {
          read = await reader.read();
        } catch (error) {
          throw this.mapAbort(error, timeoutSignal, call.signal);
        }
        if (read.done) {
          break;
        }
        if (buffer.pending.length > 1_048_576) {
          throw new GatewayError('MODEL_ERROR', 'Provider stream exceeded buffer limits.');
        }
        for (const frame of parseSseChunk(buffer, decoder.decode(read.value, { stream: true }))) {
          const parsed = safeJson(frame.data);
          if (parsed === undefined) {
            continue;
          }
          const type = typeof parsed['type'] === 'string' ? parsed['type'] : frame.event;
          if (type === 'response.output_text.delta' && typeof parsed['delta'] === 'string') {
            yield { type: 'delta', text: parsed['delta'] };
          } else if (type === 'response.completed') {
            const resp = parsed['response'] as Record<string, unknown> | undefined;
            completedText = extractOutputText(resp);
            const usage = resp?.['usage'] as Record<string, unknown> | undefined;
            yield {
              type: 'usage',
              usage: {
                inputTokens: numberOr(usage?.['input_tokens'], 0),
                outputTokens: numberOr(usage?.['output_tokens'], 0),
              },
            };
          } else if (type === 'response.incomplete') {
            // Truncated output (e.g. max_output_tokens) must never be
            // parsed or surfaced as a result.
            throw new GatewayError('MODEL_ERROR', 'Model output ended incomplete.');
          } else if (type === 'response.failed' || type === 'error') {
            throw new GatewayError('MODEL_ERROR', 'Provider reported a failed response.');
          }
        }
      }
    } finally {
      await reader.cancel().catch(() => undefined);
    }

    if (completedText === undefined) {
      throw new GatewayError('MODEL_ERROR', 'Provider stream ended without completion.');
    }
    yield { type: 'complete', text: completedText };
  }

  private mapAbort(error: unknown, timeout: AbortSignal, client: AbortSignal): Error {
    if (timeout.aborted && !client.aborted) {
      return new GatewayError('MODEL_TIMEOUT', 'Model call exceeded the time limit.');
    }
    if (client.aborted) {
      return error instanceof Error ? error : new Error('aborted');
    }
    return new GatewayError('MODEL_ERROR', 'Model call failed before completion.');
  }
}

function safeJson(text: string): Record<string, unknown> | undefined {
  try {
    const value: unknown = JSON.parse(text);
    return value !== null && typeof value === 'object' && !Array.isArray(value)
      ? (value as Record<string, unknown>)
      : undefined;
  } catch {
    return undefined;
  }
}

function numberOr(value: unknown, fallback: number): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : fallback;
}

function extractOutputText(response: Record<string, unknown> | undefined): string | undefined {
  const output = response?.['output'];
  if (!Array.isArray(output)) {
    return undefined;
  }
  const parts: string[] = [];
  for (const item of output) {
    const record = item as Record<string, unknown>;
    if (record['type'] !== 'message' || !Array.isArray(record['content'])) {
      continue;
    }
    for (const content of record['content']) {
      const contentRecord = content as Record<string, unknown>;
      if (contentRecord['type'] === 'output_text' && typeof contentRecord['text'] === 'string') {
        parts.push(contentRecord['text']);
      }
    }
  }
  return parts.length > 0 ? parts.join('') : undefined;
}
