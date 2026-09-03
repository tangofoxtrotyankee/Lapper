import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { loadOrientationSchema, validateOrientationResult } from '../src/contracts/orientation.js';

// fileURLToPath, not URL.pathname: pathname yields "/C:/..." on Windows,
// which join() then mangles into "C:\C:\...".
const fixturesRoot = fileURLToPath(new URL('../../contracts/fixtures/', import.meta.url));

function loadFixtures(kind: 'valid' | 'invalid'): { name: string; data: unknown }[] {
  const dir = join(fixturesRoot, kind);
  return readdirSync(dir)
    .filter((name) => name.endsWith('.json'))
    .map((name) => ({
      name,
      data: JSON.parse(readFileSync(join(dir, name), 'utf8')) as unknown,
    }));
}

describe('orientation schema', () => {
  it('loads and declares the expected contract', () => {
    const schema = loadOrientationSchema();
    expect(schema['title']).toBe('OrientationResult');
    expect(schema['additionalProperties']).toBe(false);
  });

  it('has at least two valid and four invalid fixtures', () => {
    expect(loadFixtures('valid').length).toBeGreaterThanOrEqual(2);
    expect(loadFixtures('invalid').length).toBeGreaterThanOrEqual(4);
  });

  describe('valid fixtures pass validation', () => {
    for (const fixture of loadFixtures('valid')) {
      it(fixture.name, () => {
        const result = validateOrientationResult(fixture.data);
        expect(result.errors).toEqual([]);
        expect(result.valid).toBe(true);
      });
    }
  });

  describe('invalid fixtures fail validation', () => {
    for (const fixture of loadFixtures('invalid')) {
      it(fixture.name, () => {
        const result = validateOrientationResult(fixture.data);
        expect(result.valid).toBe(false);
        expect(result.errors.length).toBeGreaterThan(0);
      });
    }
  });
});
