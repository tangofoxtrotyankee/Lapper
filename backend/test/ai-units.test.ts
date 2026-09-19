import { describe, expect, it } from 'vitest';
import { chooseModel } from '../src/ai/router.js';
import { deriveStrictSchema } from '../src/ai/strictSchema.js';
import { orientationResultSchema } from '../src/contracts/validators.js';
import { parseSseChunk } from '../src/gateway/openaiResponses.js';
import type { OrientRequest } from '../src/contracts/types.js';

const MODELS = { cheap: 'gpt-5.6-luna', default: 'gpt-5.6-terra', deep: 'gpt-5.6-sol' };

function request(overrides: Partial<OrientRequest['context']>, deep?: boolean): OrientRequest {
  return {
    requestId: '11111111-2222-3333-4444-555555555555',
    application: { processName: 'notepad.exe', category: 'document' },
    context: {
      selectedText: null,
      blocks: [{ id: 'b1', role: 'text', text: 'hello world' }],
      ocrText: null,
      imageIncluded: false,
      ...overrides,
    },
    ...(deep === undefined ? {} : { options: { deep } }),
    client: { version: '0.1.0', capabilities: ['uia'] },
  };
}

describe('model router', () => {
  it('routes deep mode to the deep model only when explicitly requested', () => {
    expect(chooseModel(request({}, true), MODELS).model).toBe('gpt-5.6-sol');
    expect(chooseModel(request({}, false), MODELS).model).not.toBe('gpt-5.6-sol');
  });

  it('routes short clean selections to the cheap model', () => {
    const decision = chooseModel(request({ selectedText: 'short text', blocks: [] }), MODELS);
    expect(decision.model).toBe('gpt-5.6-luna');
    expect(decision.route).toBe('cheap');
  });

  it('routes OCR-bearing requests to the default model', () => {
    const decision = chooseModel(
      request({ selectedText: 'short', blocks: [], ocrText: 'ocr content' }),
      MODELS,
    );
    expect(decision.model).toBe('gpt-5.6-terra');
  });
});

describe('strict schema derivation', () => {
  it('removes keywords unsupported by structured-outputs strict mode', () => {
    const derived = JSON.stringify(deriveStrictSchema(orientationResultSchema));
    for (const keyword of ['maxLength', 'maxItems', 'minItems', 'pattern', '"format"']) {
      expect(derived).not.toContain(`"${keyword.replaceAll('"', '')}":`);
    }
  });

  it('keeps the structural contract intact', () => {
    const derived = deriveStrictSchema(orientationResultSchema) as Record<string, unknown>;
    expect(derived['additionalProperties']).toBe(false);
    expect(derived['required']).toEqual(orientationResultSchema['required']);
    const properties = derived['properties'] as Record<string, unknown>;
    const actions = properties['suggestedActions'] as Record<string, unknown>;
    const items = actions['items'] as Record<string, unknown>;
    const type = (items['properties'] as Record<string, unknown>)['type'] as Record<
      string,
      unknown
    >;
    expect(type['enum']).toEqual([
      'copy_text',
      'read_aloud',
      'draft_text',
      'extract_facts',
      'ask_question',
      'share_text',
    ]);
  });
});

describe('SSE chunk parser', () => {
  it('parses frames split across arbitrary chunk boundaries', () => {
    const frames: { event: string; data: string }[] = [];
    const buffer = { pending: '' };
    const stream =
      'event: response.output_text.delta\ndata: {"type":"response.output_text.delta","delta":"Hel"}\n\n' +
      'event: response.output_text.delta\ndata: {"type":"response.output_text.delta","delta":"lo"}\n\n' +
      'event: response.completed\ndata: {"type":"response.completed","response":{"usage":{"input_tokens":5,"output_tokens":2}}}\n\n';
    for (let i = 0; i < stream.length; i += 7) {
      for (const frame of parseSseChunk(buffer, stream.slice(i, i + 7))) {
        frames.push(frame);
      }
    }
    expect(frames).toHaveLength(3);
    expect(frames[0]!.event).toBe('response.output_text.delta');
    expect(JSON.parse(frames[2]!.data)).toMatchObject({ type: 'response.completed' });
  });
});
