import type { FeatureFlags } from '../../../config/featureFlags';

export const onboardingStorageKey = '30s.onboarding.v1';

export type OnboardingStatus = 'new' | 'completed' | 'dismissed' | 'unavailable';
export type OnboardingStorage = Pick<Storage, 'getItem' | 'setItem'>;
export type OnboardingTarget = 'repair' | 'exploration' | 'learning';

export type OnboardingStep = {
  id: 'welcome' | OnboardingTarget;
  title: string;
  body: string;
  target?: OnboardingTarget;
};

/** Resolves browser storage, which can itself throw (privacy modes, sandboxed frames). */
export function browserStorage(): OnboardingStorage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

export function readOnboardingStatus(storage: OnboardingStorage | null): OnboardingStatus {
  if (!storage) return 'unavailable';
  try {
    const value = storage.getItem(onboardingStorageKey);
    return value === 'completed' || value === 'dismissed' ? value : 'new';
  } catch {
    return 'unavailable';
  }
}

export type WriteResult = { ok: true } | { ok: false; message: string };

export function writeOnboardingStatus(storage: OnboardingStorage | null, status: 'completed' | 'dismissed'): WriteResult {
  const failure = 'Your browser did not save this choice, so the introduction may appear again next time.';
  if (!storage) return { ok: false, message: failure };
  try {
    storage.setItem(onboardingStorageKey, status);
    return storage.getItem(onboardingStorageKey) === status ? { ok: true } : { ok: false, message: failure };
  } catch {
    return { ok: false, message: failure };
  }
}

const featureSteps: Record<OnboardingTarget, OnboardingStep> = {
  repair: {
    id: 'repair', target: 'repair', title: 'Check a symptom safely',
    body: 'Start a guided owner check. It always begins with safety and stop conditions, and you can open it at any time without finishing this introduction.',
  },
  exploration: {
    id: 'exploration', target: 'exploration', title: 'Explore the schematic',
    body: 'Open the component index to find a system or part, then focus or isolate it. The 3D view is a representative illustration, not service geometry.',
  },
  learning: {
    id: 'learning', target: 'learning', title: 'Learn how parts work',
    body: 'Open the field book or choose a part for source-scoped explanations and connected subjects.',
  },
};

/** Only steps whose controls exist in this build; an all-disabled build gets no introduction. */
export function onboardingSteps(flags: Pick<FeatureFlags, 'exploration' | 'repair' | 'learning'>): OnboardingStep[] {
  const targets = (['repair', 'exploration', 'learning'] as const).filter(target => flags[target]);
  if (targets.length === 0) return [];
  return [
    {
      id: 'welcome', title: 'Welcome to 30s',
      body: 'A practical guide to everyday car checks. This short introduction shows what this build offers. Skip it whenever you like; safety guidance never waits for it.',
    },
    ...targets.map(target => featureSteps[target]),
  ];
}
