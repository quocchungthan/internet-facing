import { describe, expect, it } from 'vitest';
import { engineLessons, vehicles } from '../../../content';
import { engineLessonItemIds } from '../../../domain/engineLessons';
import { engineLayout, placementsForVehicle, validateEngineLayoutLessons } from './engineLayout';
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
  it('prepares seven Raize placements without adding unregistered drawings to the active tree', () => {
    const placements = placementsForVehicle('raize');
    expect(placements.map(placement => placement.itemId)).toEqual(engineLessonItemIds);
    expect(placements.map(placement => placement.label)).toEqual([
      'Cylinder wall / bore', 'Piston', 'Connecting rod', 'Crankshaft',
      'Intake valve', 'Exhaust valve', 'Spark plug',
    ]);
    expect(placementsForVehicle('impala')).toEqual([]);
    expect(engineLayout).toMatchObject({
      cylinderAxis: 'y',
      crankshaftAxis: 'z',
      representativeCylinder: 'engine-cylinder-study',
    });
    expect(engineLayout.contextCylinderCenters.map(([, y]) => y).every(y => Math.abs(y - 0.1728) < 0.000001)).toBe(true);
    expect(engineLayout.contextCylinderCenters.map(([, , z]) => z)).toEqual([-0.1512, 0, 0.1512]);
    validateEngineLayoutLessons(engineLessons);

    const tree = createVehicleItems(vehicles[0]);
    expect(tree.items.map(item => item.id)).not.toEqual(expect.arrayContaining([...engineLessonItemIds]));
    expect(tree.specifications.some(spec => engineLessonItemIds.some(id => id === spec.id))).toBe(false);
  });
  it('rejects placement lessons with duplicate or missing Raize identities', () => {
    expect(() => validateEngineLayoutLessons([...engineLessons.slice(1), engineLessons[1]])).toThrow('match the seven Raize');
    expect(() => validateEngineLayoutLessons(engineLessons.slice(1))).toThrow('match the seven Raize');
  });
});
