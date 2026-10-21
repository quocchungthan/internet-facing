import { z } from 'zod';
import { vehicleSchema, type Vehicle } from '../../../domain/catalog';
import { defineOperation } from '../../../application/contracts/operation';

/** Validated query contracts over the existing bounded, statically loaded vehicle catalog. */
export function createCatalogOperations(vehicles: readonly Vehicle[]) {
  const listVehicles = defineOperation({
    name: 'catalog.listVehicles', kind: 'query', failures: [],
    input: z.object({}).strict(), output: z.array(vehicleSchema),
    execute: () => [...vehicles],
  });
  const getVehicle = defineOperation({
    name: 'catalog.getVehicle', kind: 'query', failures: [],
    input: z.object({ id: vehicleSchema.shape.id }).strict(), output: vehicleSchema.nullable(),
    execute: ({ id }) => vehicles.find(vehicle => vehicle.id === id) ?? null,
  });
  return { listVehicles, getVehicle };
}
