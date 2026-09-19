import type { FastifyInstance, FastifyReply, FastifyRequest } from 'fastify';
import type { ValidateFunction } from 'ajv/dist/2020.js';
import type { AppConfig } from '../config.js';
import {
  orientationResultSchema,
  validateActionRequest,
  validateOrientRequest,
  validateOrientationResult,
  validationMessage,
} from '../contracts/validators.js';
import type { ActionRequest, OrientRequest } from '../contracts/types.js';
import { assembleActionCall, assembleOrientCall } from '../ai/prompt.js';
import { chooseModel } from '../ai/router.js';
import { deriveStrictSchema } from '../ai/strictSchema.js';
import { GatewayError, type ModelGateway } from '../gateway/types.js';
import { SseWriter, errorEnvelope } from './sse.js';

const strictOrientationSchema = deriveStrictSchema(orientationResultSchema);

interface ContextRouteOptions {
  readonly gateway: ModelGateway;
  readonly config: AppConfig;
}

/**
 * POST /v1/context/orient and /v1/context/action — SSE streaming responses.
 *
 * Logging policy: only request ID, route, model, token counts, latency and
 * status/error codes are logged. Screen content, prompts and model output
 * must never reach the logs (CLAUDE.md).
 */
export function contextRoutes(options: ContextRouteOptions) {
  // eslint-disable-next-line @typescript-eslint/require-await
  return async function register(app: FastifyInstance): Promise<void> {
    app.post('/v1/context/orient', (request, reply) =>
      handle(request, reply, {
        ...options,
        validate: validateOrientRequest,
        structured: true,
      }),
    );
    app.post('/v1/context/action', (request, reply) =>
      handle(request, reply, {
        ...options,
        validate: validateActionRequest,
        structured: false,
      }),
    );
  };
}

interface HandleOptions extends ContextRouteOptions {
  readonly validate: ValidateFunction;
  readonly structured: boolean;
}

async function handle(
  request: FastifyRequest,
  reply: FastifyReply,
  options: HandleOptions,
): Promise<void> {
  const startedAt = process.hrtime.bigint();
  const body = request.body;

  if (!options.validate(body)) {
    await reply
      .code(400)
      .send(
        errorEnvelope(
          'INVALID_REQUEST',
          `Request failed validation: ${validationMessage(options.validate)}.`,
          headerRequestId(request),
        ),
      );
    return;
  }

  // Validated: safe to treat as our DTO shape.
  const typed = body as OrientRequest | ActionRequest;
  const requestId = typed.requestId;

  const decision = chooseModel(typed, options.config.openai.models);
  const call = options.structured
    ? assembleOrientCall(typed)
    : assembleActionCall(typed as ActionRequest);

  if (options.config.openai.apiKey === undefined) {
    request.log.info({ requestId, route: decision.route, status: 'model_unavailable' });
    await reply
      .code(503)
      .send(
        errorEnvelope(
          'MODEL_UNAVAILABLE',
          'No AI provider is configured on this server.',
          requestId,
        ),
      );
    return;
  }

  const abort = new AbortController();
  request.raw.on('close', () => {
    abort.abort();
  });

  reply.hijack();
  const sse = new SseWriter(reply);
  sse.send('accepted', { requestId, model: decision.model, route: decision.route });

  let inputTokens = 0;
  let outputTokens = 0;
  let status = 'ok';

  try {
    const stream = options.gateway.stream({
      model: decision.model,
      instructions: call.instructions,
      userInput: call.userInput,
      maxOutputTokens: options.config.openai.maxOutputTokens,
      signal: abort.signal,
      ...(options.structured ? { outputSchema: strictOrientationSchema } : {}),
    });

    for await (const event of stream) {
      if (event.type === 'delta') {
        sse.send('orientation.delta', { text: event.text });
      } else if (event.type === 'usage') {
        inputTokens = event.usage.inputTokens;
        outputTokens = event.usage.outputTokens;
      } else {
        // complete
        if (options.structured) {
          const parsed = safeParse(event.text);
          if (parsed === undefined || !validateOrientationResult(parsed)) {
            status = 'model_output_invalid';
            sse.send(
              'error',
              errorEnvelope(
                'MODEL_OUTPUT_INVALID',
                'The model returned an invalid result.',
                requestId,
              ).error,
            );
            continue;
          }
          sse.send('orientation.complete', { requestId });
          sse.send('result', parsed);
        } else {
          sse.send('orientation.complete', { requestId });
          sse.send('result', { text: event.text });
        }
      }
    }
  } catch (error) {
    if (abort.signal.aborted) {
      status = 'client_cancelled';
    } else if (error instanceof GatewayError) {
      status = error.code.toLowerCase();
      sse.send('error', errorEnvelope(error.code, error.message, requestId).error);
    } else {
      status = 'internal_error';
      sse.send(
        'error',
        errorEnvelope('INTERNAL_ERROR', 'The request failed unexpectedly.', requestId).error,
      );
    }
  }

  const latencyMs = Number((process.hrtime.bigint() - startedAt) / 1_000_000n);
  sse.send('usage', {
    model: decision.model,
    route: decision.route,
    inputTokens,
    outputTokens,
    latencyMs,
  });
  sse.end();

  // Metadata only — never content.
  request.log.info({
    requestId,
    route: decision.route,
    model: decision.model,
    status,
    latencyMs,
    inputTokens,
    outputTokens,
    blocks: typed.context.blocks.length,
  });
}

function headerRequestId(request: FastifyRequest): string {
  const header = request.headers['x-lapper-request-id'];
  return typeof header === 'string' ? header.slice(0, 64) : 'unknown';
}

function safeParse(text: string): unknown {
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}
