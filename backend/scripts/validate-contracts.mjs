// Validates the shared contracts package:
//  1. contracts/openapi.yaml is a valid OpenAPI 3.1 document
//  2. contracts/orientation.schema.json compiles as JSON Schema 2020-12
//  3. every fixtures/valid/*.json passes and every fixtures/invalid/*.json fails
// Exits non-zero on any violation. Used locally and by the CI contracts job.
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Validator } from '@seriousme/openapi-schema-validator';
import { Ajv2020 } from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';

const contractsDir = fileURLToPath(new URL('../../contracts/', import.meta.url));
let failures = 0;

function fail(message) {
  failures += 1;
  console.error(`FAIL: ${message}`);
}

// 1. OpenAPI 3.1 document
const openapiValidator = new Validator();
await openapiValidator.addSpecRef(
  join(contractsDir, 'orientation.schema.json'),
  './orientation.schema.json',
);
const openapiResult = await openapiValidator.validate(join(contractsDir, 'openapi.yaml'));
if (openapiResult.valid) {
  console.log(`ok: openapi.yaml is valid OpenAPI ${openapiValidator.version}`);
} else {
  fail(`openapi.yaml invalid: ${JSON.stringify(openapiResult.errors, null, 2)}`);
}

// 2. Orientation schema compiles
const ajv = new Ajv2020({ allErrors: true, strict: true });
addFormats(ajv);
const schema = JSON.parse(readFileSync(join(contractsDir, 'orientation.schema.json'), 'utf8'));
let validate;
try {
  validate = ajv.compile(schema);
  console.log('ok: orientation.schema.json compiles (JSON Schema 2020-12, strict)');
} catch (error) {
  fail(`orientation.schema.json does not compile: ${String(error)}`);
}

// 3. Fixtures
if (validate) {
  for (const kind of ['valid', 'invalid']) {
    const dir = join(contractsDir, 'fixtures', kind);
    const files = readdirSync(dir).filter((name) => name.endsWith('.json'));
    if (files.length === 0) {
      fail(`no ${kind} fixtures found in ${dir}`);
    }
    for (const name of files) {
      const data = JSON.parse(readFileSync(join(dir, name), 'utf8'));
      const valid = validate(data);
      if (kind === 'valid' && !valid) {
        fail(`fixtures/valid/${name} should validate: ${ajv.errorsText(validate.errors)}`);
      } else if (kind === 'invalid' && valid) {
        fail(`fixtures/invalid/${name} should NOT validate but passed`);
      } else {
        console.log(`ok: fixtures/${kind}/${name}`);
      }
    }
  }
}

if (failures > 0) {
  console.error(`${failures} contract validation failure(s)`);
  process.exit(1);
}
console.log('All contract validations passed.');
