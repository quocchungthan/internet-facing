import type { Vehicle } from '../../../domain/catalog';
import { createVehicleItems } from './items';

export function readSubject(search: string, vehicle: Vehicle) {
  const params = new URLSearchParams(search);
  const tree = createVehicleItems(vehicle);
  const requested = params.get('item') ?? params.get('part') ?? 'vehicle';
  const item = tree.items.find(candidate => candidate.id === requested);
  return {
    item: item ?? tree.items[0],
    isolated: Boolean(item && item.id !== 'vehicle' && params.get('isolate') === '1'),
    invalid: !item,
  };
}

export function subjectSearch(search: string, vehicle: Vehicle, itemId: string, isolated: boolean) {
  const item = createVehicleItems(vehicle).items.find(candidate => candidate.id === itemId);
  if (!item) throw new Error(`Unknown selected item ${itemId}.`);
  const params = new URLSearchParams(search);
  params.set('car', vehicle.id);
  params.delete('part');
  params.delete('item');
  params.delete('isolate');
  if (item.partId) params.set('part', item.partId);
  if (item.id !== 'vehicle' && item.id !== item.partId) params.set('item', item.id);
  if (isolated && item.id !== 'vehicle') params.set('isolate', '1');
  return params.toString();
}
