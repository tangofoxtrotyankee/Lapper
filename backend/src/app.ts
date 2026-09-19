import Fastify, {
  LogController,
  type FastifyBaseLogger,
  type FastifyInstance,
  type FastifyServerOptions,
} from 'fastify';
import { loadConfig, type AppConfig } from './config.js';
import { healthRoutes } from './routes/health.js';
import { contextRoutes } from './routes/context.js';
import { OpenAiResponsesGateway } from './gateway/openaiResponses.js';
import type { ModelGateway } from './gateway/types.js';
import { headerRequestId, logMeta } from './logging/meta.js';

/**
 * Fixed client-facing messages per error class. Never forward
 * `error.message` for client-caused errors: Node's JSON.parse SyntaxError
 * embeds a snippet of the request body, which may contain screen content.
 */
const FIXED_ERROR_MESSAGES: Record<string, string> = {
  INVALID_REQUEST: 'The request body is not valid.',
  CONTEXT_TOO_LARGE: 'The request body is too large.',
  UNSUPPORTED_MEDIA_TYPE: 'Requests must be application/json.',
  INTERNAL_ERROR: 'The request failed unexpectedly.',
};

export interface BuildAppOptions {
  readonly config?: AppConfig;
  readonly logger?: boolean;
  /** Injectable for tests; defaults to the OpenAI Responses adapter. */
  readonly gateway?: ModelGateway;
  /** Test hook: a pino-compatible logger instance (overrides `logger`). */
  readonly loggerInstance?: unknown;
}

/**
 * Builds the Fastify application without binding a socket.
 *
 * Logging policy (CLAUDE.md): operational metadata only. Request bodies,
 * screen content and model responses must never be logged; body logging is
 * disabled and must stay disabled.
 */
export function buildApp(options: BuildAppOptions = {}): FastifyInstance {
  const config = options.config ?? loadConfig();
  const gateway = options.gateway ?? new OpenAiResponsesGateway(config.openai);

  const serverOptions: FastifyServerOptions = {
    bodyLimit: config.bodyLimitBytes,
    logController: new LogController({ disableRequestLogging: true }),
  };
  if (options.loggerInstance !== undefined) {
    serverOptions.loggerInstance = options.loggerInstance as FastifyBaseLogger;
  } else {
    serverOptions.logger = options.logger ?? false;
  }
  const app = Fastify(serverOptions);

  app.register(healthRoutes);
  app.register(contextRoutes({ gateway, config }));

  app.setErrorHandler((error, request, reply) => {
    const raw = (error as { statusCode?: unknown }).statusCode;
    const statusCode = typeof raw === 'number' && raw >= 400 ? raw : 500;
    const code =
      statusCode === 413
        ? 'CONTEXT_TOO_LARGE'
        : statusCode === 415
          ? 'UNSUPPORTED_MEDIA_TYPE'
          : statusCode < 500
            ? 'INVALID_REQUEST'
            : 'INTERNAL_ERROR';
    const requestId = headerRequestId(request);
    request.log.info(logMeta({ requestId, status: code, statusCode }));
    void reply.code(statusCode).send({
      error: { code, message: FIXED_ERROR_MESSAGES[code], requestId },
    });
  });

  return app;
}
