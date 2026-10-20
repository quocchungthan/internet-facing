import { describe, expect, it } from 'vitest';
import { vehicles } from '../../../content';
import { createVehicleItems, subtreeIds, validateItems, worldPosition, type ItemTree } from './items';

describe('vehicle item hierarchy', () => {
  it.each(vehicles)('keeps engine descendants and repeated occurrences distinct for $id', vehicle => {
    const tree = createVehicleItems(vehicle);
    expect([...subtreeIds(tree, 'engine')]).toEqual(expect.arrayContaining(['engine-block', 'engine-head', 'engine-cover', 'intake', 'lubrication']));
    expect(subtreeIds(tree, 'engine').has('wheels')).toBe(false);
    const wheels = tree.items.filter(item => item.specificationId === 'wheel');
    expect(wheels).toHaveLength(4);
    expect(new Set(wheels.map(item => item.id)).size).toBe(4);
    const intake = tree.items.find(item => item.id === 'intake')!;
    expect(worldPosition(tree, intake)).toEqual(vehicle.parts.find(part => part.id === 'intake')!.position);
    expect(subtreeIds(tree, 'vehicle').size).toBe(tree.items.length);
  });
  it('rejects invalid references, duplicate IDs, roots and cycles', () => {
    const tree = createVehicleItems(vehicles[0]);
    const invalid = (change: (tree: ItemTree) => void) => {
      const copy = structuredClone(tree);
      change(copy);
      return () => validateItems(copy);
    };
    expect(invalid(t => { t.items[1].parentId = 'missing'; })).toThrow('Unknown parent');
    expect(invalid(t => { t.items[1].specificationId = 'missing'; })).toThrow('Unknown specification');
    expect(invalid(t => { t.items.push(t.items[0]); })).toThrow('Duplicate');
    expect(invalid(t => { t.items[1].parentId = undefined; })).toThrow('one item root');
    expect(invalid(t => { t.items[1].parentId = 'engine'; })).toThrow('cycle');
  });
});
