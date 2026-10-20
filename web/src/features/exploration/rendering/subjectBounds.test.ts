import { expect, it } from 'vitest';
import { BoxGeometry, Group, Mesh, MeshBasicMaterial } from 'three';
import { fitDistance, subjectBounds } from './subjectBounds';

it('fits a narrow viewport farther away and scales to an isolated component', () => {
  expect(fitDistance(2, 38, 0.5)).toBeGreaterThan(fitDistance(2, 38, 1.6));
  expect(fitDistance(0.2, 38, 1)).toBeCloseTo(fitDistance(2, 38, 1) / 10);
  expect(() => fitDistance(0, 38, 1)).toThrow();
});
it('measures only selected items in world coordinates, not unrelated geometry', () => {
  const model = new Group();
  for (const [id, x] of [['engine', 5], ['wheel', -20]] as const) {
    const group = new Group();
    group.userData.itemId = id;
    group.position.x = x;
    group.add(new Mesh(new BoxGeometry(2, 2, 2), new MeshBasicMaterial()));
    model.add(group);
  }
  const sphere = subjectBounds(model, new Set(['engine']));
  expect(sphere.center.x).toBe(5);
  expect(sphere.radius).toBeCloseTo(Math.sqrt(3));
  expect(() => subjectBounds(model, new Set(['missing']))).toThrow('no drawable geometry');
});
