import { describe, expect, it, vi } from 'vitest';
import { z } from 'zod';
import { defineOperation, OperationError, runOperation } from './operation';

const partSchema = z.object({ id: z.string(), label: z.string(), drawable: z.boolean() }).strict();
const parts = [{ id: 'engine', label: 'Engine', drawable: true }];

function getPart(execute = vi.fn((input: { id: string }) => {
  const part = parts.find(p => p.id === input.id);
  if (!part) throw new OperationError('not_found', `Part ${input.id} not found.`);
  return part;
})) {
  return {
    execute,
    operation: defineOperation({
      name: 'catalog.getPart', kind: 'query', failures: ['not_found'],
      input: z.object({ id: z.string().min(1) }).strict(), output: partSchema, execute,
    }),
  };
}

describe('runOperation', () => {
  it('returns a valid success value unchanged and records contract version 1', async () => {
    const { operation } = getPart();
    expect(operation.version).toBe(1);
    await expect(runOperation(operation, { id: 'engine' })).resolves.toEqual({ ok: true, value: parts[0] });
  });

  it('rejects invalid input without invoking execution', async () => {
    const { operation, execute } = getPart();
    const result = await runOperation(operation, { id: '', extra: true });
    expect(execute).not.toHaveBeenCalled();
    expect(result).toMatchObject({ ok: false, error: { code: 'invalid_argument', operation: 'catalog.getPart' } });
    if (!result.ok) expect(result.error.issues?.map(i => i.path.join('.'))).toEqual(expect.arrayContaining(['id']));
  });

  it('reports invalid output as an explicit contract violation', async () => {
    const { operation } = getPart(vi.fn(() => ({ id: 'engine', label: 'Engine' }) as never));
    const result = await runOperation(operation, { id: 'engine' });
    expect(result).toMatchObject({ ok: false, error: { code: 'contract_violation' } });
    if (!result.ok) expect(result.error.issues?.[0].path).toEqual(['drawable']);
  });

  it('reports declared domain failures by code', async () => {
    const { operation } = getPart();
    await expect(runOperation(operation, { id: 'missing' })).resolves.toEqual({
      ok: false, error: { code: 'not_found', operation: 'catalog.getPart', message: 'Part missing not found.' },
    });
  });

  it('treats undeclared domain failures as contract violations', async () => {
    const { operation } = getPart(vi.fn(() => { throw new OperationError('conflict', 'busy'); }));
    const result = await runOperation(operation, { id: 'engine' });
    expect(result).toMatchObject({ ok: false, error: { code: 'contract_violation' } });
  });

  it('reports unexpected exceptions as internal failures, never success', async () => {
    const boom = new TypeError('broken rule');
    const { operation } = getPart(vi.fn(() => { throw boom; }));
    const result = await runOperation(operation, { id: 'engine' });
    expect(result).toMatchObject({ ok: false, error: { code: 'internal', cause: boom } });
  });

  it('does not validate or execute when the signal is already aborted', async () => {
    const { operation, execute } = getPart();
    const controller = new AbortController();
    controller.abort();
    const result = await runOperation(operation, { id: '' }, { signal: controller.signal });
    expect(execute).not.toHaveBeenCalled();
    expect(result).toMatchObject({ ok: false, error: { code: 'cancelled' } });
    if (!result.ok) expect(result.error.message).toContain('not rolled back');
  });

  it('propagates the signal to execution and reports abort during execution as cancelled', async () => {
    const controller = new AbortController();
    let received: AbortSignal | undefined;
    const operation = defineOperation({
      name: 'repair.advanceStep', kind: 'mutation', failures: [],
      input: z.object({ step: z.string() }), output: z.string(),
      execute: (_input, { signal }) => {
        received = signal;
        return new Promise<string>((_, reject) => signal.addEventListener('abort', () => reject(new Error('aborted'))));
      },
    });
    const pending = runOperation(operation, { step: 'prepare' }, { signal: controller.signal });
    controller.abort();
    await expect(pending).resolves.toMatchObject({ ok: false, error: { code: 'cancelled' } });
    expect(received).toBe(controller.signal);
  });

  it('rejects operation names that are not dotted domain paths', () => {
    expect(() => defineOperation({
      name: 'GET /parts', kind: 'query', failures: [], input: z.object({}), output: z.null(), execute: () => null,
    })).toThrow(/dotted lowerCamel/);
  });
});
