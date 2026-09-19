/**
 * Derives an OpenAI structured-outputs (strict mode) compatible variant of a
 * contract JSON Schema. Strict mode supports only a keyword subset; length
 * and size constraints are dropped for the model and re-enforced server-side
 * by validating the final output against the FULL contract schema with Ajv.
 */

const UNSUPPORTED_KEYWORDS = new Set([
  'maxLength',
  'minLength',
  'pattern',
  'format',
  'maxItems',
  'minItems',
  'minimum',
  'maximum',
  '$schema',
  '$id',
]);

export function deriveStrictSchema(schema: unknown): unknown {
  if (Array.isArray(schema)) {
    return schema.map(deriveStrictSchema);
  }
  if (schema !== null && typeof schema === 'object') {
    const out: Record<string, unknown> = {};
    for (const [key, value] of Object.entries(schema)) {
      if (UNSUPPORTED_KEYWORDS.has(key)) {
        continue;
      }
      out[key] = deriveStrictSchema(value);
    }
    return out;
  }
  return schema;
}
