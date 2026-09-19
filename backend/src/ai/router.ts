import type { ModelRoutes } from '../config.js';
import type { OrientRequest, ActionRequest } from '../contracts/types.js';

export type RouteName = 'cheap' | 'default' | 'deep';

export interface RouteDecision {
  readonly route: RouteName;
  readonly model: string;
  readonly reason: string;
}

const CHEAP_MAX_CHARS = 1500;

function totalTextLength(request: OrientRequest | ActionRequest): number {
  const blocks = request.context.blocks.reduce((sum, b) => sum + b.text.length, 0);
  return (
    blocks + (request.context.selectedText?.length ?? 0) + (request.context.ocrText?.length ?? 0)
  );
}

/**
 * Configuration-driven model routing (docs/02-architecture.md):
 * deep only when the user explicitly asked; cheap for small clean-text
 * selections; default otherwise. Observable via the returned reason.
 */
export function chooseModel(
  request: OrientRequest | ActionRequest,
  models: ModelRoutes,
): RouteDecision {
  if (request.options?.deep === true) {
    return { route: 'deep', model: models.deep, reason: 'user_selected_deep' };
  }

  const selected = request.context.selectedText;
  if (
    selected !== undefined &&
    selected !== null &&
    selected.length > 0 &&
    totalTextLength(request) <= CHEAP_MAX_CHARS &&
    (request.context.ocrText === undefined || request.context.ocrText === null)
  ) {
    return { route: 'cheap', model: models.cheap, reason: 'short_clean_selection' };
  }

  return { route: 'default', model: models.default, reason: 'default' };
}
