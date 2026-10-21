import { describe, expect, it } from 'vitest';
import { readFeatureFlags } from './featureFlags';

describe('build-time feature configuration', () => {
  it('preserves all features when no flags are supplied', () => {
    expect(readFeatureFlags({})).toEqual({ exploration: true, repair: true, learning: true, onboarding: true });
  });

  it.each(Array.from({ length: 16 }, (_, mask) => mask))('keeps each feature independent for mask %i', mask => {
    const expected = {
      exploration: Boolean(mask & 4), repair: Boolean(mask & 2), learning: Boolean(mask & 1),
      onboarding: Boolean(mask & 8),
    };
    expect(readFeatureFlags({
      VITE_FEATURE_EXPLORATION: String(expected.exploration),
      VITE_FEATURE_REPAIR: String(expected.repair),
      VITE_FEATURE_LEARNING: String(expected.learning),
      VITE_FEATURE_ONBOARDING: String(expected.onboarding),
    })).toEqual(expected);
  });

  it.each(['', '0', '1', 'FALSE', 'yes', false])('rejects an ambiguous flag %j', value => {
    expect(() => readFeatureFlags({ VITE_FEATURE_REPAIR: value }))
      .toThrow('VITE_FEATURE_REPAIR must be "true" or "false".');
  });
});
