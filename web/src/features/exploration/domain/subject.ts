import type { Vehicle } from '../../../domain/catalog';
import { createVehicleItems, specificationFor, subtreeIds, type ItemTree } from './items';

export type SubjectActivation =
  | { kind: 'focus'; itemId: string }
  | { kind: 'information-only'; itemId: string; reason: string }
  | { kind: 'unavailable'; reason: string };

export function activateSubject(tree: ItemTree, itemId: string): SubjectActivation {
  const item = tree.items.find(candidate => candidate.id === itemId);
  if (!item) return { kind: 'unavailable', reason: 'This subject is not in the active vehicle. Choose another item from the component index.' };
  const descendants = subtreeIds(tree, itemId);
  const drawable = tree.items.some(candidate => descendants.has(candidate.id)
    && specificationFor(tree, candidate).drawing !== 'assembly');
  return drawable
    ? { kind: 'focus', itemId }
    : { kind: 'information-only', itemId, reason: 'This subject has no 3D drawing. Use the component index to explore available geometry.' };
}

export function readSubject(search: string, vehicle: Vehicle) {
  const params = new URLSearchParams(search);
  const tree = createVehicleItems(vehicle);
  const requested = params.get('item') ?? params.get('part') ?? 'vehicle';
  const activation = activateSubject(tree, requested);
  const item = activation.kind === 'unavailable' ? tree.items[0] : tree.items.find(candidate => candidate.id === requested)!;
  return {
    item,
    isolated: activation.kind === 'focus' && item.id !== 'vehicle' && params.get('isolate') === '1',
    invalid: activation.kind === 'unavailable',
    activation,
  };
}

export function subjectSearch(search: string, vehicle: Vehicle, itemId: string, isolated: boolean) {
  const tree = createVehicleItems(vehicle);
  const activation = activateSubject(tree, itemId);
  if (activation.kind === 'unavailable') throw new Error(`Unknown selected item ${itemId}.`);
  const item = tree.items.find(candidate => candidate.id === itemId)!;
  const params = new URLSearchParams(search);
  params.set('car', vehicle.id);
  params.delete('part');
  params.delete('item');
  params.delete('isolate');
  if (item.partId) params.set('part', item.partId);
  if (item.id !== 'vehicle' && item.id !== item.partId) params.set('item', item.id);
  if (isolated && activation.kind === 'focus' && item.id !== 'vehicle') params.set('isolate', '1');
  return params.toString();
}
