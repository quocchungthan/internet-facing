import { describe, expect, it } from 'vitest';
import { advanceHingeAngle } from './hingeMotion';

describe('advanceHingeAngle', () => {
  it('advances toward the target over normal-motion frames', () => {
    const next = advanceHingeAngle(0, 1, false, 1 / 60);
    expect(next).toBeGreaterThan(0);
    expect(next).toBeLessThan(1);
  });

  it('caps a long frame so the hinge does not snap after idle', () => {
    expect(advanceHingeAngle(0, 1, false, 5)).toBe(advanceHingeAngle(0, 1, false, 1 / 30));
  });

  it('jumps directly to the target when reduced motion is enabled', () => {
    expect(advanceHingeAngle(0, 1, true, 1 / 60)).toBe(1);
  });
});
