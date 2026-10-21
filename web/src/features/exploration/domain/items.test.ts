import { describe, expect, it } from 'vitest';
import { engineLessons, vehicles } from '../../../content';
import { engineLessonItemIds } from '../../../domain/engineLessons';
import { engineLayout, placementsForVehicle, validateEngineLayoutLessons } from './engineLayout';
import { createVehicleItems, itemLabel, searchItemTree, subtreeIds, validateEngineLessonsAgainstItems, validateItems,
  worldPosition, type ItemTree } from './items';

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
  it('prepares seven Raize placements in the shared layout', () => {
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

  });
  it('rejects placement lessons with duplicate or missing Raize identities', () => {
    expect(() => validateEngineLayoutLessons([...engineLessons.slice(1), engineLessons[1]])).toThrow('match the seven Raize');
    expect(() => validateEngineLayoutLessons(engineLessons.slice(1))).toThrow('match the seven Raize');
  });
  it('mounts every roster subject only with a real drawing entry', () => {
    const raize = createVehicleItems(vehicles.find(vehicle => vehicle.id === 'raize')!);
    const studyIds = new Set<string>(engineLessonItemIds);
    const studies = raize.items.filter(item => studyIds.has(item.id));
    expect(studies.map(item => item.id)).toEqual(engineLessonItemIds);
    expect(studies.every(item => item.parentId === 'engine' && item.partId === 'engine')).toBe(true);
    expect(studies.map(item => item.position)).toEqual(placementsForVehicle('raize').map(item => item.position));
    const valves = studies.filter(item => item.id.includes('valve'));
    expect(valves).toHaveLength(2);
    expect(valves[0].specificationId).toBe(valves[1].specificationId);
    expect(raize.specifications.find(spec => spec.id === valves[0].specificationId)?.drawing).toBe('engine-valve');
    expect(raize.specifications.find(spec => spec.id === 'engine-spark-plug-study')?.drawing).toBe('engine-spark-plug');
    expect(valves.map(item => itemLabel(raize, item))).toEqual(['Intake valve', 'Exhaust valve']);
    validateEngineLessonsAgainstItems(engineLessons, raize.items);
    const impala = createVehicleItems(vehicles.find(vehicle => vehicle.id === 'impala')!);
    expect(impala.items.some(item => studyIds.has(item.id))).toBe(false);
    expect(() => validateEngineLessonsAgainstItems(engineLessons, raize.items.filter(item => item.id !== 'engine-piston-study')))
      .toThrow('Missing mounted engine lesson subject');
  });
  it('searches exact occurrences by normalized label, curated aliases and ancestry', () => {
    const raize = vehicles.find(vehicle => vehicle.id === 'raize')!;
    const tree = createVehicleItems(raize);
    expect(searchItemTree(tree, raize, engineLessons, ' PISTON ROD ').map(result => result.itemId))
      .toEqual(['engine-rod-study']);
    expect(searchItemTree(tree, raize, engineLessons, 'bore').map(result => result.itemId))
      .toEqual(['engine-cylinder-study']);
    expect(searchItemTree(tree, raize, engineLessons, 'piston crown').map(result => result.itemId))
      .toEqual(['engine-piston-study']);
    expect(searchItemTree(tree, raize, engineLessons, 'powertrain')
      .some(result => result.itemId === 'engine-rod-study')).toBe(true);
    expect(searchItemTree(tree, raize, engineLessons, 'nothing matches')).toEqual([]);
  });
  it('retains repeated wheel identities, system filters and drawable status', () => {
    const impala = vehicles.find(vehicle => vehicle.id === 'impala')!;
    const tree = createVehicleItems(impala);
    const wheels = searchItemTree(tree, impala, engineLessons, 'wheel', 'chassis')
      .filter(result => result.itemId.startsWith('wheel-'));
    expect(wheels.map(result => result.itemId)).toEqual([
      'wheel-front-left', 'wheel-front-right', 'wheel-rear-left', 'wheel-rear-right',
    ]);
    expect(new Set(wheels.map(result => result.label)).size).toBe(4);
    expect(searchItemTree(tree, impala, engineLessons, '', 'powertrain')
      .every(result => result.system === 'powertrain')).toBe(true);
    const assembly = searchItemTree(tree, impala, engineLessons, 'powertrain system')
      .find(result => result.itemId === 'powertrain-assembly');
    expect(assembly).toMatchObject({ drawable: false, reason: 'No drawing available' });
    expect(searchItemTree(tree, impala, engineLessons, '')).toHaveLength(tree.items.length - 1);
  });
});
