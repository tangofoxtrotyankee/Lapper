import Fastify, { LogController, type FastifyInstance } from 'fastify';
import { loadConfig, type AppConfig } from './config.js';
import { healthRoutes } from './routes/health.js';

export interface BuildAppOptions {
  readonly config?: AppConfig;
  readonly logger?: boolean;
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

  const app = Fastify({
    logger: options.logger ?? false,
    bodyLimit: config.bodyLimitBytes,
    logController: new LogController({ disableRequestLogging: true }),
  });

  app.register(healthRoutes);

  return app;
}
