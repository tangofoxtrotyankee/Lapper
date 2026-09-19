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

  return app;
}
