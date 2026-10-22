import { describe, expect, it } from 'vitest';
import { vehicles } from '../../../content';
import { matchVehicleIdentity, parseIdentityPackages, vehicleIdentityPackageSchema, type VehicleIdentity } from './vehicleIdentity';

function identity(id = 'raize'): VehicleIdentity {
  return {
    id, kind: 'supported-configuration', make: 'Toyota', model: 'Raize',
    generation: 'first-generation', modelYears: { from: 2019, to: 2019 },
    productionSpan: { from: null, to: null }, market: 'Japan',
    engine: '1KR-VET', transmission: 'D-CVT', drive: '2WD', variant: 'Z',
  };
}

describe('versioned identity-only packages', () => {
  it('represents information-only metadata with explicit unknowns and no drawing/book requirement', () => {
    const partial = { ...identity('information-only'), generation: null, modelYears: null, market: null, variant: null };
    const parsed = vehicleIdentityPackageSchema.parse({ schemaVersion: 1, revision: 'draft-1', identity: partial });
    expect(parsed.identity.market).toBeNull();
    expect(matchVehicleIdentity(parsed.identity, identity())).toBe('partial');
  });

  it('retains both current catalog URL IDs without equating the catalog with a physical vehicle', () => {
    const packages = parseIdentityPackages(vehicles.map(car => ({
      schemaVersion: 1, revision: 'legacy-id-fixture',
      identity: {
        id: car.id, kind: 'supported-configuration', make: null, model: null, generation: null,
        modelYears: null, productionSpan: { from: null, to: null }, market: null,
        engine: null, transmission: null, drive: null, variant: null,
      },
    })));
    expect(packages.map(entry => entry.identity.id)).toEqual(vehicles.map(car => car.id));
    expect(() => vehicleIdentityPackageSchema.parse({
      schemaVersion: 1, revision: '1', identity: { ...identity(), kind: 'physical-vehicle' },
    })).toThrow();
  });

  it('rejects unsupported versions, duplicate identities, reversed dates/years and unmodelled claims', () => {
    const entry = { schemaVersion: 1, revision: '1', identity: identity() };
    expect(() => parseIdentityPackages([entry, entry])).toThrow('Duplicate vehicle identity ID');
    expect(() => vehicleIdentityPackageSchema.parse({ ...entry, schemaVersion: 2 })).toThrow();
    expect(() => vehicleIdentityPackageSchema.parse({ ...entry, approvedRepair: true })).toThrow();
    for (const invalid of [
      { ...identity(), modelYears: { from: 2020, to: 2019 } },
      { ...identity(), productionSpan: { from: '2020-01-01', to: '2019-01-01' } },
      { ...identity(), productionSpan: { from: '2020-02-30', to: null } },
      { ...identity(), buildDate: '2019-01-01' },
      { ...identity(), market: '' },
    ]) {
      expect(() => vehicleIdentityPackageSchema.parse({ ...entry, identity: invalid })).toThrow();
    }
  });
});

describe('conservative identity matching', () => {
  it('normalizes case, Unicode composition and whitespace but not market/variant meaning', () => {
    const left = { ...identity(), variant: ' Cà  Z ' };
    const right = { ...identity('another-id'), variant: 'ca\u0300 z' };
    expect(matchVehicleIdentity(left, right)).toBe('exact');
    for (const change of [{ market: 'US' }, { variant: 'G' }, { engine: 'other-engine' }, { drive: '4WD' }, { model: 'Rai-ze' }]) {
      expect(matchVehicleIdentity(identity(), { ...identity(), ...change })).toBe('different');
    }
  });

  it('never treats unknown market or variant as exact verified fitment, including unknown against itself', () => {
    for (const field of ['market', 'variant', 'generation', 'engine'] as const) {
      const unknown = { ...identity(), [field]: null };
      expect(matchVehicleIdentity(identity(), unknown)).toBe('partial');
      expect(matchVehicleIdentity(unknown, unknown)).toBe('partial');
    }
  });

  it('keeps model-year overlap partial and production dates independent from model year', () => {
    const reference = identity();
    expect(matchVehicleIdentity(reference, { ...reference, modelYears: { from: 2019, to: 2021 } })).toBe('partial');
    expect(matchVehicleIdentity(reference, { ...reference, modelYears: { from: 2020, to: 2021 } })).toBe('different');
    expect(matchVehicleIdentity(reference, { ...reference, productionSpan: { from: '2018-10-01', to: '2024-01-01' } })).toBe('exact');
    expect(matchVehicleIdentity(reference, { ...reference, market: null, engine: 'different' })).toBe('different');
  });
});
