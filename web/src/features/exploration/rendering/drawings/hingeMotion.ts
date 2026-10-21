import { MathUtils } from 'three';

export function advanceHingeAngle(current: number, target: number, reduced: boolean, dt: number): number {
  if (reduced) return target;
  const next = MathUtils.damp(current, target, 8, Math.min(dt, 1 / 30));
  return Math.abs(next - target) < 0.001 ? target : next;
}
