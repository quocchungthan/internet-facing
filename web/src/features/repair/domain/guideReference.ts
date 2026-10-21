import { z } from 'zod';
import type { Vehicle } from '../../../domain/catalog';

const isoDate = z.string().regex(/^\d{4}-\d{2}-\d{2}$/).refine(value => !Number.isNaN(Date.parse(`${value}T00:00:00Z`)), 'Invalid date');

export const guideReferenceSchema = z.object({
  guideId: z.string().regex(/^[a-z][a-z0-9-]*$/),
  source: z.object({ title: z.string().min(1), url: z.string().url() }),
  scope: z.string().min(1),
  reviewed: isoDate,
  applicability: z.discriminatedUnion('status', [
    z.object({ status: z.literal('generic'), appliesTo: z.string().min(1) }),
    z.object({
      status: z.literal('verified'),
      vehicleIds: z.array(z.string().min(1)).min(1),
      markets: z.array(z.string().min(1)).min(1),
      evidence: z.string().min(1),
    }),
  ]),
}).strict();

export type GuideReference = z.infer<typeof guideReferenceSchema>;

export function parseGuideReference(input: unknown): GuideReference {
  const result = guideReferenceSchema.safeParse(input);
  if (!result.success) {
    throw new Error(`Invalid guide reference: ${result.error.issues.map(issue => `${issue.path.join('.') || '(root)'} ${issue.message}`).join('; ')}`);
  }
  return result.data;
}

export const reviewMaxAgeDays = 365;

export type ResolvedApplicability = {
  kind: 'generic' | 'verified' | 'unknown';
  label: string;
  detail: string;
  stale: boolean;
};

/** Never upgrades to an exact-model claim unless verified evidence names this vehicle and is current. */
export function resolveApplicability(reference: GuideReference, vehicle: Pick<Vehicle, 'id' | 'market'>, now: Date): ResolvedApplicability {
  const ageDays = (now.getTime() - Date.parse(`${reference.reviewed}T00:00:00Z`)) / 86_400_000;
  const stale = ageDays > reviewMaxAgeDays || ageDays < 0;
  const { applicability } = reference;
  if (applicability.status === 'generic') {
    return {
      kind: 'generic', stale,
      label: 'Generic guidance · not verified for this model',
      detail: `Applies to ${applicability.appliesTo}. Not verified for the ${vehicle.market} vehicle shown.`,
    };
  }
  if (!applicability.vehicleIds.includes(vehicle.id)) {
    return {
      kind: 'unknown', stale,
      label: 'Applicability unknown for this vehicle',
      detail: 'The reviewed evidence covers other vehicles only. Treat this as general information and use the manual for your vehicle.',
    };
  }
  if (stale) {
    return {
      kind: 'unknown', stale,
      label: 'Applicability unknown · review out of date',
      detail: `Verification dated ${reference.reviewed} needs a fresh review before it can be treated as model-specific.`,
    };
  }
  return {
    kind: 'verified', stale,
    label: 'Verified for this vehicle',
    detail: `${applicability.evidence} Markets: ${applicability.markets.join(', ')}.`,
  };
}

export type RecallLookup = { jurisdiction: string; title: string; url: string };

export const officialRecallLookups: readonly RecallLookup[] = [
  { jurisdiction: 'United Kingdom', title: 'GOV.UK: Check if a vehicle has been recalled', url: 'https://www.gov.uk/check-vehicle-recall' },
  { jurisdiction: 'United States', title: 'NHTSA: Safety issues and recalls', url: 'https://www.nhtsa.gov/recalls' },
];
