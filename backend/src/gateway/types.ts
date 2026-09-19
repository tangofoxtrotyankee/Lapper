/**
 * ModelGateway: the only abstraction through which model providers are
 * reached (docs/02-architecture.md). The gateway is pure transport — prompt
 * assembly and routing happen outside so they are independently testable.
 */

export interface TokenUsage {
  readonly inputTokens: number;
  readonly outputTokens: number;
}

export type GatewayEvent =
  | { readonly type: 'delta'; readonly text: string }
  | { readonly type: 'complete'; readonly text: string }
  | { readonly type: 'usage'; readonly usage: TokenUsage };

export interface GatewayCall {
  readonly model: string;
  /** Trusted instructions. */
  readonly instructions: string;
  /** User input containing the untrusted screen payload. */
  readonly userInput: string;
  /** Strict-mode JSON schema for structured output; omitted => plain text. */
  readonly outputSchema?: unknown;
  readonly maxOutputTokens: number;
  readonly signal: AbortSignal;
}

export class GatewayError extends Error {
  constructor(
    /** Machine-readable code for the client error envelope. */
    readonly code: 'MODEL_UNAVAILABLE' | 'MODEL_TIMEOUT' | 'MODEL_ERROR',
    /** Metadata-safe message: must never contain screen or model content. */
    message: string,
  ) {
    super(message);
    this.name = 'GatewayError';
  }
}

export interface ModelGateway {
  stream(call: GatewayCall): AsyncGenerator<GatewayEvent, void, void>;
}
