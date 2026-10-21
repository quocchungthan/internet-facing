import { describe, expect, it } from 'vitest';
import { onboardingStorageKey, onboardingSteps, readOnboardingStatus, writeOnboardingStatus } from './onboarding';

function memoryStorage() {
  const values = new Map<string, string>();
  return {
    getItem: (key: string) => values.get(key) ?? null,
    setItem: (key: string, value: string) => { values.set(key, value); },
    clear: () => values.clear(),
  };
}

describe('first-visit onboarding state', () => {
  it('is new until completed or dismissed, and new again after storage is cleared', () => {
    const storage = memoryStorage();
    expect(readOnboardingStatus(storage)).toBe('new');
    expect(writeOnboardingStatus(storage, 'dismissed')).toEqual({ ok: true });
    expect(readOnboardingStatus(storage)).toBe('dismissed');
    expect(writeOnboardingStatus(storage, 'completed')).toEqual({ ok: true });
    expect(readOnboardingStatus(storage)).toBe('completed');
    storage.clear();
    expect(readOnboardingStatus(storage)).toBe('new');
  });

  it('treats unknown stored values as a first visit', () => {
    const storage = memoryStorage();
    storage.setItem(onboardingStorageKey, 'maybe');
    expect(readOnboardingStatus(storage)).toBe('new');
  });

  it('reports unavailable storage and failed writes without claiming completion', () => {
    expect(readOnboardingStatus(null)).toBe('unavailable');
    expect(writeOnboardingStatus(null, 'completed').ok).toBe(false);
    const throwing = { getItem: () => { throw new Error('denied'); }, setItem: () => { throw new Error('quota'); } };
    expect(readOnboardingStatus(throwing)).toBe('unavailable');
    expect(writeOnboardingStatus(throwing, 'dismissed')).toMatchObject({ ok: false, message: expect.stringMatching(/did not save/) });
    const ignoring = { getItem: () => null, setItem: () => undefined };
    expect(writeOnboardingStatus(ignoring, 'completed').ok).toBe(false);
  });
});

describe('onboarding step selection', () => {
  it.each(Array.from({ length: 8 }, (_, mask) => mask))('targets only enabled features for mask %i', mask => {
    const flags = { exploration: Boolean(mask & 4), repair: Boolean(mask & 2), learning: Boolean(mask & 1) };
    const steps = onboardingSteps(flags);
    const targets = steps.flatMap(step => step.target ? [step.target] : []);
    expect(targets).toEqual((['repair', 'exploration', 'learning'] as const).filter(target => flags[target]));
    expect(steps.length).toBe(mask === 0 ? 0 : targets.length + 1);
    if (mask) expect(steps[0].id).toBe('welcome');
  });
});
