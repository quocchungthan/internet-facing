import { z } from 'zod';

const knownText = z.string().trim().min(1).nullable();
const year = z.number().int().min(1886).max(9999);
const yearRange = z.object({ from: year, to: year }).strict().refine(range => range.from <= range.to, 'Reversed model-year range.');
const productionSpan = z.object({
  from: z.string().date().nullable(),
  to: z.string().date().nullable(),
}).strict().refine(span => !span.from || !span.to || span.from <= span.to, 'Reversed production span.');

export const vehicleIdentitySchema = z.object({
  id: z.string().regex(/^[a-z][a-z0-9-]*$/),
  kind: z.literal('supported-configuration'),
  make: knownText,
  model: knownText,
  generation: knownText,
  modelYears: yearRange.nullable(),
  productionSpan,
  market: knownText,
  engine: knownText,
  transmission: knownText,
  drive: knownText,
  variant: knownText,
}).strict();

export const vehicleIdentityPackageSchema = z.object({
  schemaVersion: z.literal(1),
  revision: z.string().trim().min(1),
  identity: vehicleIdentitySchema,
}).strict();

export type VehicleIdentity = z.infer<typeof vehicleIdentitySchema>;
export type VehicleIdentityPackage = z.infer<typeof vehicleIdentityPackageSchema>;
export type IdentityMatch = 'exact' | 'partial' | 'different';
const matchingFields = ['make', 'model', 'generation', 'market', 'engine', 'transmission', 'drive', 'variant'] as const;

export function normalizeIdentityText(value: string): string {
  return value.normalize('NFC').trim().replace(/\s+/g, ' ').toLowerCase();
}

// A partial match is a discovery candidate, never exact fitment or a duplicate key.
export function matchVehicleIdentity(left: VehicleIdentity, right: VehicleIdentity): IdentityMatch {
  let partial = false;
  for (const field of matchingFields) {
    const a = left[field];
    const b = right[field];
    if (a === null || b === null) partial = true;
    else if (normalizeIdentityText(a) !== normalizeIdentityText(b)) return 'different';
  }
  const a = left.modelYears;
  const b = right.modelYears;
  if (!a || !b) return 'partial';
  if (a.to < b.from || b.to < a.from) return 'different';
  if (a.from !== a.to || b.from !== b.to || a.from !== b.from) partial = true;
  return partial ? 'partial' : 'exact';
}

export function parseIdentityPackages(input: unknown): VehicleIdentityPackage[] {
  const packages = z.array(vehicleIdentityPackageSchema).parse(input);
  const ids = packages.map(entry => entry.identity.id);
  if (new Set(ids).size !== ids.length) throw new Error('Duplicate vehicle identity ID.');
  return packages;
}
