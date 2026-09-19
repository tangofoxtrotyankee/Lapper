export interface ModelRoutes {
  /** Cheap/simple clean-text tasks. */
  readonly cheap: string;
  /** Default production model. */
  readonly default: string;
  /** Explicit Deep mode only. */
  readonly deep: string;
}

export interface OpenAiConfig {
  /** Absent means AI endpoints return MODEL_UNAVAILABLE; health stays green. */
  readonly apiKey: string | undefined;
  readonly baseUrl: string;
  readonly models: ModelRoutes;
  /** Hard cap on a single model call, ms. */
  readonly requestTimeoutMs: number;
  /** Ceiling on model output tokens (abuse/cost control). */
  readonly maxOutputTokens: number;
}

export interface AppConfig {
  readonly host: string;
  readonly port: number;
  /** Maximum accepted request body size in bytes. Requests above this are rejected. */
  readonly bodyLimitBytes: number;
  readonly openai: OpenAiConfig;
}

const DEFAULT_PORT = 3000;
const DEFAULT_BODY_LIMIT_BYTES = 512 * 1024;
const DEFAULT_MODEL_TIMEOUT_MS = 60_000;
const DEFAULT_MAX_OUTPUT_TOKENS = 2048;

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
    openai: {
      apiKey: env['OPENAI_API_KEY'] === '' ? undefined : env['OPENAI_API_KEY'],
      baseUrl: env['OPENAI_BASE_URL'] ?? 'https://api.openai.com/v1',
      models: {
        cheap: env['OPENAI_MODEL_CHEAP'] ?? 'gpt-5.6-luna',
        default: env['OPENAI_MODEL_DEFAULT'] ?? 'gpt-5.6-terra',
        deep: env['OPENAI_MODEL_DEEP'] ?? 'gpt-5.6-sol',
      },
      requestTimeoutMs: DEFAULT_MODEL_TIMEOUT_MS,
      maxOutputTokens: DEFAULT_MAX_OUTPUT_TOKENS,
    },
  };
}
