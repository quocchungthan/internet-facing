import { describe, expect, it } from 'vitest';
import { runOperation } from '../../../application/contracts/operation';
import { advanceWiperGuide, wiperSteps, type WiperStepId } from '../domain/wiperGuide';
import { advanceWiper } from './wiperOperations';

describe('repair.wiper.advance', () => {
  it('matches the domain transition for every available choice', async () => {
    for (const step of Object.keys(wiperSteps) as WiperStepId[]) {
      for (let choice = 0; choice < wiperSteps[step].choices.length; choice++) {
        await expect(runOperation(advanceWiper, { step, choice }))
          .resolves.toEqual({ ok: true, value: { step: advanceWiperGuide(step, choice) } });
      }
    }
  });

  it('reports unavailable choices, including terminal steps, as invalid arguments', async () => {
    for (const input of [{ step: 'resolved', choice: 0 }, { step: 'prepare', choice: 2 }]) {
      await expect(runOperation(advanceWiper, input)).resolves.toMatchObject({ ok: false, error: { code: 'invalid_argument' } });
    }
  });

  it('rejects unknown steps and malformed choices with schema issues', async () => {
    for (const input of [{ step: 'diagnose', choice: 0 }, { step: 'prepare', choice: -1 },
      { step: 'prepare', choice: 0.5 }, { step: 'prepare' }, { step: 'prepare', choice: 0, history: [] }]) {
      const result = await runOperation(advanceWiper, input);
      expect(result).toMatchObject({ ok: false, error: { code: 'invalid_argument' } });
      if (!result.ok) expect(result.error.issues?.length).toBeGreaterThan(0);
    }
  });

  it('is cancelled before the transition when the signal is already aborted', async () => {
    const controller = new AbortController();
    controller.abort();
    await expect(runOperation(advanceWiper, { step: 'prepare', choice: 0 }, { signal: controller.signal }))
      .resolves.toMatchObject({ ok: false, error: { code: 'cancelled' } });
  });
});
