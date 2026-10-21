import { z } from 'zod';

const id = z.string().regex(/^[a-z][a-z0-9-]*$/);
const vector = z.tuple([z.number(), z.number(), z.number()]);
const source = z.object({
  id,
  title: z.string().min(1),
  url: z.string().url(),
  scope: z.string().min(1),
});
const photo = z.object({
  id,
  src: z.string().startsWith('/media/'),
  title: z.string(),
  author: z.string(),
  license: z.string(),
  licenseUrl: z.string().url(),
  sourceUrl: z.string().url(),
  note: z.string(),
});
const part = z.object({
  id,
  label: z.string(),
  system: z.enum(['powertrain', 'chassis', 'electrical', 'body']),
  summary: z.string().min(1),
  description: z.string().min(1),
  position: vector,
  checks: z.array(z.string()).min(1),
  symptoms: z.array(z.string()).min(1),
  caution: z.string().min(1),
  sourceIds: z.array(id).min(1),
  relatedIds: z.array(id),
  photoIds: z.array(id),
});
export const vehicleSchema = z.object({
  id,
  name: z.string(),
  subtitle: z.string(),
  year: z.number().int(),
  market: z.string(),
  engine: z.string(),
  transmission: z.string(),
  drive: z.string(),
  character: z.string(),
  color: z.string().regex(/^#[0-9a-f]{6}$/i),
  geometry: z.enum(['compact-suv', 'classic-sedan']),
  dimensions: z.object({ length: z.number().positive(), width: z.number().positive(), wheelbase: z.number().positive() }),
  facts: z.array(z.object({ label: z.string(), value: z.string(), sourceId: id })),
  book: z.string().regex(/^\/books\/[a-z0-9-]+\.md$/),
  sources: z.array(source).min(1),
  photos: z.array(photo).min(1),
  parts: z.array(part).min(1),
});
export type Vehicle = z.infer<typeof vehicleSchema>;
export type Part = Vehicle['parts'][number];
export type Photo = Vehicle['photos'][number];
export type System = Part['system'];
export type ViewMode = 'exterior' | 'xray' | 'exploded';
export const schematicPartIds = [
  'engine', 'lubrication', 'intake', 'cooling', 'transmission', 'brakes',
  'wheels', 'suspension', 'electrical', 'fuel', 'exhaust', 'body',
] as const;

export function parseVehicle(input: unknown): Vehicle {
  const vehicle = vehicleSchema.parse(input);
  const unique = (values: string[], kind: string) => {
    if (new Set(values).size !== values.length) throw new Error(`Duplicate ${kind} in ${vehicle.id}`);
  };
  unique(vehicle.parts.map(p => p.id), 'part ID');
  unique(vehicle.sources.map(s => s.id), 'source ID');
  unique(vehicle.photos.map(p => p.id), 'photo ID');
  for (const required of schematicPartIds) {
    if (!vehicle.parts.some(p => p.id === required)) {
      throw new Error(`Missing schematic part "${required}" in ${vehicle.id}`);
    }
  }
  const requireReference = (values: string[], known: string[], kind: string) => {
    for (const value of values) {
      if (!known.includes(value)) throw new Error(`Unknown ${kind} "${value}" in ${vehicle.id}`);
    }
  };
  const sourceIds = vehicle.sources.map(s => s.id);
  for (const part of vehicle.parts) {
    requireReference(part.sourceIds, sourceIds, 'source');
    requireReference(part.relatedIds, vehicle.parts.map(p => p.id), 'part');
    requireReference(part.photoIds, vehicle.photos.map(p => p.id), 'photo');
  }
  requireReference(vehicle.facts.map(f => f.sourceId), sourceIds, 'fact source');
  return vehicle;
}

export function readSelection(search: string, vehicles: Vehicle[]) {
  const params = new URLSearchParams(search);
  const vehicle = vehicles.find(v => v.id === params.get('car')) ?? vehicles[0];
  if (!vehicle) throw new Error('The vehicle catalog is empty.');
  const requestedPart = params.get('part');
  return {
    vehicle,
    part: vehicle.parts.find(p => p.id === requestedPart),
    invalid: (params.has('car') && params.get('car') !== vehicle.id)
      || (requestedPart !== null && !vehicle.parts.some(p => p.id === requestedPart)),
  };
}
