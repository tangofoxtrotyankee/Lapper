export interface AppConfig {
  readonly host: string;
  readonly port: number;
  /** Maximum accepted request body size in bytes. Requests above this are rejected. */
  readonly bodyLimitBytes: number;
}

const DEFAULT_PORT = 3000;
const DEFAULT_BODY_LIMIT_BYTES = 256 * 1024;

function parsePort(value: string | undefined): number {
  if (value === undefined || value === '') {
    return DEFAULT_PORT;
  }
  const parsed = Number.parseInt(value, 10);
  if (!Number.isInteger(parsed) || parsed < 1 || parsed > 65535) {
    throw new Error(`Invalid PORT value: ${value}`);
  }
  return parsed;
}

export function loadConfig(env: NodeJS.ProcessEnv = process.env): AppConfig {
  return {
    host: env['HOST'] ?? '127.0.0.1',
    port: parsePort(env['PORT']),
    bodyLimitBytes: DEFAULT_BODY_LIMIT_BYTES,
  };
}
