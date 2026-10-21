import { expect, it } from 'vitest';
import { vehicles } from '../../../content';
import { engineLessonItemIds } from '../../../domain/engineLessons';
import { activateSubject, readSubject, subjectSearch } from './subject';
import type { ItemTree } from './items';

it('preserves old part links and shares a specific isolated occurrence', () => {
  expect(readSubject('?part=engine', vehicles[0]).item.id).toBe('engine');
  const search = subjectSearch('?other=keep', vehicles[0], 'wheel-front-left', true);
  const result = readSubject(search, vehicles[0]);
  expect(result.item.id).toBe('wheel-front-left');
  expect(result.isolated).toBe(true);
  expect(search).toContain('other=keep');
  expect(search).toContain('part=wheels');
});
it('rejects unknown subjects and resets root isolation', () => {
  expect(readSubject('?item=missing&isolate=1', vehicles[0])).toMatchObject({
    invalid: true, isolated: false, item: { id: 'vehicle' }, activation: { kind: 'unavailable' },
  });
  expect(subjectSearch('?part=engine&item=engine&isolate=1', vehicles[1], 'vehicle', true)).toBe('car=impala');
  expect(() => subjectSearch('', vehicles[0], 'missing', false)).toThrow('Unknown selected item');
});
it.each(engineLessonItemIds)('focuses the exact geometry-backed subject %s', itemId => {
  const search = subjectSearch('?keep=1', vehicles.find(vehicle => vehicle.id === 'raize')!, itemId, true);
  expect(search).toContain(`item=${itemId}`);
  expect(search).toContain('isolate=1');
  expect(readSubject(search, vehicles[0])).toMatchObject({
    item: { id: itemId }, isolated: true, activation: { kind: 'focus', itemId },
  });
});
it('reports information-only and unavailable subjects without claiming focus', () => {
  const tree: ItemTree = {
    items: [
      { id: 'vehicle', specificationId: 'vehicle', position: [0, 0, 0] },
      { id: 'reference', specificationId: 'reference', parentId: 'vehicle', position: [0, 0, 0] },
    ],
    specifications: [
      { id: 'vehicle', label: 'Vehicle', drawing: 'assembly' },
      { id: 'reference', label: 'Reference record', drawing: 'assembly' },
    ],
  };
  expect(activateSubject(tree, 'reference')).toMatchObject({ kind: 'information-only', itemId: 'reference' });
  expect(activateSubject(tree, 'missing')).toMatchObject({ kind: 'unavailable' });
});
