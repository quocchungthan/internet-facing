import { describe, expect, it } from 'vitest';
import { vehicles } from '../../../content';
import { runOperation } from '../../../application/contracts/operation';
import type { Vehicle } from '../../../domain/catalog';
import { createCatalogOperations } from './catalogOperations';

const { listVehicles, getVehicle } = createCatalogOperations(vehicles);

describe('catalog operations', () => {
  it('lists the existing bounded catalog unchanged and in order', async () => {
    await expect(runOperation(listVehicles, {})).resolves.toEqual({ ok: true, value: vehicles });
  });

  it('returns the existing vehicle for a known identifier', async () => {
    for (const vehicle of vehicles) {
      await expect(runOperation(getVehicle, { id: vehicle.id })).resolves.toEqual({ ok: true, value: vehicle });
    }
  });

  it('returns null, not a failure, for a well-formed missing identifier', async () => {
    await expect(runOperation(getVehicle, { id: 'corolla' })).resolves.toEqual({ ok: true, value: null });
  });

  it('rejects malformed identifiers and unexpected fields as invalid arguments', async () => {
    for (const input of [{}, { id: 42 }, { id: 'raize', page: 2 }, { id: 'Raize Z!' }]) {
      await expect(runOperation(getVehicle, input)).resolves.toMatchObject({ ok: false, error: { code: 'invalid_argument' } });
    }
    await expect(runOperation(listVehicles, { cursor: 'x' })).resolves.toMatchObject({ ok: false, error: { code: 'invalid_argument' } });
  });

  it('reports catalog data that violates the vehicle contract', async () => {
    const broken = [{ ...vehicles[0], year: 'unknown' } as unknown as Vehicle];
    const result = await runOperation(createCatalogOperations(broken).listVehicles, {});
    expect(result).toMatchObject({ ok: false, error: { code: 'contract_violation' } });
  });

  it('is cancelled for a pre-aborted signal', async () => {
    const controller = new AbortController();
    controller.abort();
    await expect(runOperation(getVehicle, { id: 'raize' }, { signal: controller.signal }))
      .resolves.toMatchObject({ ok: false, error: { code: 'cancelled' } });
  });
});
