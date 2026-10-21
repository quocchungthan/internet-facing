import { describe, expect, it } from 'vitest';
import { officialRecallLookups, parseGuideReference, resolveApplicability } from './guideReference';
import { wiperReference } from './wiperGuide';

const raize = { id: 'raize', market: 'Japan · launch specification' };
const reviewedDay = new Date('2026-10-21T00:00:00Z');

const verified = {
  guideId: 'example',
  source: { title: 'Example', url: 'https://example.org/manual' },
  scope: 'Example scope',
  reviewed: '2026-10-01',
  applicability: { status: 'verified', vehicleIds: ['raize'], markets: ['Japan'], evidence: 'Owner manual section 1.' },
};

describe('guide source and applicability', () => {
  it('labels the wiper guide as generic with source, scope and review date', () => {
    expect(wiperReference.source.url).toMatch(/^https:\/\//);
    expect(wiperReference.scope.length).toBeGreaterThan(0);
    const resolved = resolveApplicability(wiperReference, raize, reviewedDay);
    expect(resolved).toMatchObject({ kind: 'generic', stale: false, label: 'Generic guidance · not verified for this model' });
  });

  it('never upgrades verified evidence for another vehicle or a stale review to an exact-model claim', () => {
    const reference = parseGuideReference(verified);
    expect(resolveApplicability(reference, raize, reviewedDay).kind).toBe('verified');
    expect(resolveApplicability(reference, { id: 'impala', market: 'US' }, reviewedDay).kind).toBe('unknown');
    const late = new Date('2027-11-01T00:00:00Z');
    expect(resolveApplicability(reference, raize, late)).toMatchObject({ kind: 'unknown', stale: true });
    expect(resolveApplicability(wiperReference, raize, late)).toMatchObject({ kind: 'generic', stale: true });
  });

  it('rejects missing or malformed metadata explicitly', () => {
    expect(() => parseGuideReference({ ...verified, reviewed: '2026-13-45' })).toThrow(/reviewed/);
    expect(() => parseGuideReference({ ...verified, applicability: { status: 'verified', vehicleIds: [], markets: ['JP'], evidence: 'x' } }))
      .toThrow(/applicability.vehicleIds/);
    const withoutScope: Record<string, unknown> = { ...verified };
    delete withoutScope.scope;
    expect(() => parseGuideReference(withoutScope)).toThrow(/scope/);
    expect(() => parseGuideReference({ ...verified, applicability: { status: 'exact' } })).toThrow(/Invalid guide reference/);
  });

  it('lists only external official recall services', () => {
    for (const lookup of officialRecallLookups) expect(lookup.url).toMatch(/^https:\/\/www\.(gov\.uk|nhtsa\.gov)\//);
  });
});
