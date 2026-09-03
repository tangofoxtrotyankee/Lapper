import type { FastifyInstance } from 'fastify';

const healthResponseSchema = {
  type: 'object',
  additionalProperties: false,
  required: ['status'],
  properties: {
    status: { type: 'string' },
  },
} as const;

// eslint-disable-next-line @typescript-eslint/require-await
export async function healthRoutes(app: FastifyInstance): Promise<void> {
  app.get('/health/live', { schema: { response: { 200: healthResponseSchema } } }, () => ({
    status: 'live',
  }));

  // Phase 0 has no external dependencies (no database, no AI provider), so
  // readiness is equivalent to liveness. Later phases add dependency checks.
  app.get('/health/ready', { schema: { response: { 200: healthResponseSchema } } }, () => ({
    status: 'ready',
  }));
}
