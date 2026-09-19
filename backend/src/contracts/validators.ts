import { readFileSync } from 'node:fs';
import { Ajv2020, type ValidateFunction } from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';

const contractsDir = new URL('../../../contracts/', import.meta.url);

function loadSchema(name: string): Record<string, unknown> {
  return JSON.parse(readFileSync(new URL(name, contractsDir), 'utf8')) as Record<string, unknown>;
}

const ajv = new Ajv2020({ allErrors: true, strict: true });
addFormats.default(ajv);

export const orientationResultSchema = loadSchema('orientation.schema.json');

export const validateOrientRequest: ValidateFunction = ajv.compile(
  loadSchema('orient-request.schema.json'),
);
export const validateActionRequest: ValidateFunction = ajv.compile(
  loadSchema('action-request.schema.json'),
);
export const validateOrientationResult: ValidateFunction = ajv.compile(orientationResultSchema);

export function validationMessage(validate: ValidateFunction): string {
  // Instance paths only — never echo captured screen content back in errors.
  return (validate.errors ?? [])
    .slice(0, 5)
    .map((e) => `${e.instancePath || '/'} ${e.keyword}`)
    .join('; ');
}
