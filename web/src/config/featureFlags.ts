export type FeatureFlags = Readonly<{
  exploration: boolean;
  repair: boolean;
  learning: boolean;
  onboarding: boolean;
}>;

function readFlag(env: Record<string, unknown>, name: string): boolean {
  const value = env[name];
  if (value === undefined || value === 'true') return true;
  if (value === 'false') return false;
  throw new Error(`${name} must be "true" or "false".`);
}

export function readFeatureFlags(env: Record<string, unknown>): FeatureFlags {
  return {
    exploration: readFlag(env, 'VITE_FEATURE_EXPLORATION'),
    repair: readFlag(env, 'VITE_FEATURE_REPAIR'),
    learning: readFlag(env, 'VITE_FEATURE_LEARNING'),
    onboarding: readFlag(env, 'VITE_FEATURE_ONBOARDING'),
  };
}
