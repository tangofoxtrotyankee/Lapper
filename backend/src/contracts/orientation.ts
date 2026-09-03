import { readFileSync } from 'node:fs';
import { Ajv2020, type ValidateFunction, type ErrorObject } from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';

const schemaUrl = new URL('../../../contracts/orientation.schema.json', import.meta.url);

export interface OrientationValidationResult {
  readonly valid: boolean;
  readonly errors: readonly ErrorObject[];
}

let compiled: ValidateFunction | undefined;

export function loadOrientationSchema(): Record<string, unknown> {
  return JSON.parse(readFileSync(schemaUrl, 'utf8')) as Record<string, unknown>;
}

function getValidator(): ValidateFunction {
  if (compiled === undefined) {
    const ajv = new Ajv2020({ allErrors: true, strict: true });
    addFormats.default(ajv);
    compiled = ajv.compile(loadOrientationSchema());
  }
  return compiled;
}

/** Validates a candidate orientation result against the shared contract schema. */
export function validateOrientationResult(candidate: unknown): OrientationValidationResult {
  const validator = getValidator();
  const valid = validator(candidate);
  return { valid, errors: validator.errors ?? [] };
}
