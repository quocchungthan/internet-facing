import { describe, expect, it } from 'vitest';
import { advanceWiperGuide, wiperSteps } from './wiperGuide';
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
