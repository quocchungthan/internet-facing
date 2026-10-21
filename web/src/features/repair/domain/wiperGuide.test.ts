import { describe, expect, it } from 'vitest';
import { advanceWiperGuide, compareWiperObservations, wiperOutcome, wiperSteps } from './wiperGuide';
import { createVehicleItems } from '../../exploration/domain/items';
import { vehicles } from '../../../content';

describe('wiper troubleshooting', () => {
  it('checks cleaning before a remedy and verifies the observation', () => {
    expect(advanceWiperGuide('prepare', 0)).toBe('inspect');
    expect(advanceWiperGuide('inspect', 1)).toBe('clean');
    expect(advanceWiperGuide('clean', 0)).toBe('verifyClean');
    expect(advanceWiperGuide('verifyClean', 0)).toBe('resolved');
  });

  it('routes damage to fitment and persistent problems to service, not endless replacement', () => {
    expect(advanceWiperGuide('inspect', 0)).toBe('replace');
    expect(advanceWiperGuide('verifyClean', 1)).toBe('replace');
    expect(advanceWiperGuide('replace', 0)).toBe('verifyReplace');
    expect(advanceWiperGuide('verifyReplace', 1)).toBe('service');
    expect(advanceWiperGuide('prepare', 1)).toBe('stop');
    expect(() => advanceWiperGuide('resolved', 0)).toThrow('Invalid wiper-guide choice');
  });

  it('records improved, unresolved, worse and not-tested outcomes only from verification', () => {
    const base = [{ step: 'prepare', choice: 0 }, { step: 'inspect', choice: 1 }, { step: 'clean', choice: 0 }] as const;
    expect(wiperOutcome(base)).toBeNull();
    expect(wiperOutcome([...base, { step: 'verifyClean', choice: 0 }])).toBe('improved');
    expect(wiperOutcome([...base, { step: 'verifyClean', choice: 1 }])).toBe('unresolved');
    expect(wiperOutcome([...base, { step: 'verifyClean', choice: 3 }])).toBe('worse');
    expect(wiperOutcome([...base, { step: 'verifyClean', choice: 4 }])).toBe('not-tested');
    expect(wiperOutcome([...base, { step: 'verifyClean', choice: 1 }, { step: 'replace', choice: 0 }, { step: 'verifyReplace', choice: 0 }]))
      .toBe('improved');
    expect(advanceWiperGuide('verifyClean', 3)).toBe('service');
    expect(advanceWiperGuide('verifyReplace', 2)).toBe('service');
    expect(advanceWiperGuide('verifyReplace', 3)).toBe('notTested');
  });

  it('compares before and after only under parked wet-glass conditions', () => {
    const before = { patterns: ['streaks' as const], side: 'driver' as const, parkedWetGlass: true };
    const after = { patterns: [], side: 'driver' as const, parkedWetGlass: true };
    expect(compareWiperObservations(before, after)).toEqual({
      comparable: true, summary: 'Before: streaks (driver side). After: no visible problem (driver side).',
    });
    expect(compareWiperObservations(before, { ...after, parkedWetGlass: false }).comparable).toBe(false);
    expect(compareWiperObservations(null, after).comparable).toBe(false);
  });

  it('links every check to a real item on each available vehicle', () => {
    for (const vehicle of vehicles) {
      const tree = createVehicleItems(vehicle);
      for (const step of Object.values(wiperSteps)) {
        expect(tree.items.some(item => item.id === step.target)).toBe(true);
        for (const choice of step.choices) expect(choice.next in wiperSteps).toBe(true);
      }
    }
  });
});
