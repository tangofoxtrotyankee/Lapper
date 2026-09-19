import type { FastifyRequest } from 'fastify';

/**
 * The only fields operational logs may carry (CLAUDE.md privacy rules).
 * Anything else passed to logMeta() is silently dropped, so an accidental
 * `logMeta({ body })` or `logMeta({ text: delta })` logs nothing.
 */
const ALLOWED_KEYS: ReadonlySet<string> = new Set([
  'requestId',
  'route',
  'model',
  'status',
  'statusCode',
  'latencyMs',
  'inputTokens',
  'outputTokens',
  'blocks',
  'errorCode',
]);

const MAX_STRING_LENGTH = 64;

/** Whitelist picker: the only sanctioned way to build a log object. */
export function logMeta(
  fields: Record<string, string | number | boolean | undefined>,
): Record<string, string | number | boolean> {
  const out: Record<string, string | number | boolean> = {};
  for (const [key, value] of Object.entries(fields)) {
    if (value === undefined || !ALLOWED_KEYS.has(key)) {
      continue;
    }
    if (typeof value === 'string' && value.length > MAX_STRING_LENGTH) {
      continue;
    }
    out[key] = value;
  }
  return out;
}

/** Correlation id from the client header, bounded; never request content. */
export function headerRequestId(request: FastifyRequest): string {
  const header = request.headers['x-lapper-request-id'];
  return typeof header === 'string' ? header.slice(0, 64) : 'unknown';
}
