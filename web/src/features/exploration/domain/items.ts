import type { Vehicle } from '../../../domain/catalog';
import { engineLessonItemIds, type EngineLesson } from '../../../domain/engineLessons';
import { placementsForVehicle } from './engineLayout';

export type Vector = [number, number, number];
export type DrawingKind = 'assembly' | 'body' | 'engine-block' | 'engine-head' | 'engine-cover'
  | 'engine-cylinder-wall' | 'engine-piston' | 'engine-rod' | 'engine-crankshaft'
  | 'engine-valve' | 'engine-spark-plug'
  | 'lubrication' | 'intake' | 'cooling' | 'transmission' | 'wheel' | 'brake'
  | 'suspension' | 'electrical' | 'fuel' | 'exhaust' | 'wiper-arm' | 'wiper-blade' | 'wiper-rubber';
export type ItemSpecification = { id: string; label: string; drawing: DrawingKind };
export type Item = {
  id: string; specificationId: string; parentId?: string; partId?: string; position: Vector;
};
export type ItemTree = { items: Item[]; specifications: ItemSpecification[] };

export function validateItems(tree: ItemTree): ItemTree {
  const ids = new Set(tree.items.map(item => item.id));
  const specs = new Set(tree.specifications.map(spec => spec.id));
  if (ids.size !== tree.items.length || specs.size !== tree.specifications.length) {
    throw new Error('Duplicate item or specification ID.');
  }
  if (tree.items.filter(item => !item.parentId).length !== 1) throw new Error('Expected one item root.');
  for (const item of tree.items) {
    if (!specs.has(item.specificationId)) throw new Error(`Unknown specification for ${item.id}.`);
    validateAncestors(item, tree.items);
  }
  return tree;
}

function validateAncestors(item: Item, items: Item[]) {
  const visited = new Set([item.id]);
  let parentId = item.parentId;
  while (parentId) {
    if (visited.has(parentId)) throw new Error(`Item hierarchy cycle at ${parentId}.`);
    visited.add(parentId);
    const parent = items.find(candidate => candidate.id === parentId);
    if (!parent) throw new Error(`Unknown parent ${parentId}.`);
    parentId = parent.parentId;
  }
}

export function subtreeIds(tree: ItemTree, id: string): Set<string> {
  if (!tree.items.some(item => item.id === id)) throw new Error(`Unknown item ${id}.`);
  const result = new Set([id]);
  const queue = [id];
  for (let index = 0; index < queue.length; index++) {
    for (const child of tree.items.filter(item => item.parentId === queue[index])) {
      if (result.has(child.id)) throw new Error(`Item hierarchy cycle at ${child.id}.`);
      result.add(child.id);
      queue.push(child.id);
    }
  }
  return result;
}

export function specificationFor(tree: ItemTree, item: Item): ItemSpecification {
  const spec = tree.specifications.find(candidate => candidate.id === item.specificationId);
  if (!spec) throw new Error(`Unknown specification ${item.specificationId}.`);
  return spec;
}

export function validateEngineLessonsAgainstItems(lessons: readonly EngineLesson[], items: readonly Item[]) {
  const itemIds = new Set(items.map(item => item.id));
  for (const itemId of engineLessonItemIds) {
    if (!itemIds.has(itemId)) throw new Error(`Missing mounted engine lesson subject ${itemId}.`);
  }
  for (const lesson of lessons) {
    if (!itemIds.has(lesson.itemId)) throw new Error(`Missing mounted engine lesson subject ${lesson.itemId}.`);
    for (const connection of lesson.connections) {
      if (!itemIds.has(connection.itemId)) {
        throw new Error(`Missing mounted connection ${connection.itemId} in ${lesson.itemId}.`);
      }
    }
  }
}

export function itemLabel(tree: ItemTree, item: Item): string {
  if (item.id === 'engine-intake-valve-study') return 'Intake valve';
  if (item.id === 'engine-exhaust-valve-study') return 'Exhaust valve';
  const label = specificationFor(tree, item).label;
  const location = item.id.match(/-(front|rear)-(left|right)$/);
  if (location) return `${label} (${location[1]} ${location[2]})`;
  const wiperSide = item.id.match(/^wiper-(?:arm|blade|rubber)-(left|right)$/);
  return wiperSide ? `${label} (${wiperSide[1]})` : label;
}

export function worldPosition(tree: ItemTree, item: Item): Vector {
  const result: Vector = [...item.position];
  let parent = tree.items.find(candidate => candidate.id === item.parentId);
  while (parent) {
    const offset = parent.position;
    result.forEach((value, axis) => { result[axis] = value + offset[axis]; });
    parent = tree.items.find(candidate => candidate.id === parent?.parentId);
  }
  return result;
}

const partDrawings: Record<string, DrawingKind> = {
  engine: 'assembly', lubrication: 'lubrication', intake: 'intake', cooling: 'cooling',
  transmission: 'transmission', brakes: 'assembly', wheels: 'assembly',
  suspension: 'assembly', electrical: 'electrical', fuel: 'fuel', exhaust: 'exhaust', body: 'body',
};

const engineLessonDrawings = [
  ['engine-cylinder-study', 'engine-cylinder-wall'],
  ['engine-piston-study', 'engine-piston'],
  ['engine-rod-study', 'engine-rod'],
  ['engine-crankshaft-study', 'engine-crankshaft'],
  ['engine-intake-valve-study', 'engine-valve'],
  ['engine-exhaust-valve-study', 'engine-valve'],
  ['engine-spark-plug-study', 'engine-spark-plug'],
] as const satisfies readonly (readonly [typeof engineLessonItemIds[number], DrawingKind])[];

type AddItem = (
  id: string, label: string, drawing: DrawingKind, parentId?: string,
  position?: Vector, partId?: string, specificationId?: string
) => void;

export function createVehicleItems(vehicle: Vehicle): ItemTree {
  const items: Item[] = [];
  const specifications: ItemSpecification[] = [];
  const add = (id: string, label: string, drawing: DrawingKind, parentId?: string,
    position: Vector = [0, 0, 0], partId?: string, specificationId = id) => {
    items.push({ id, specificationId, parentId, position, partId });
    if (!specifications.some(spec => spec.id === specificationId)) {
      specifications.push({ id: specificationId, label, drawing });
    }
  };
  add('vehicle', vehicle.name, 'assembly');
  for (const system of ['powertrain', 'chassis', 'electrical', 'body']) {
    add(`${system}-assembly`, `${system[0].toUpperCase()}${system.slice(1)} system`, 'assembly', 'vehicle');
  }
  for (const part of vehicle.parts) {
    const drawing = partDrawings[part.id];
    if (!drawing) throw new Error(`No drawing specification for ${part.id}.`);
    const parentId = ['intake', 'lubrication'].includes(part.id) ? 'engine' : `${part.system}-assembly`;
    const parentPosition = parentId === 'engine' ? vehicle.parts.find(p => p.id === 'engine')!.position : [0, 0, 0];
    const position: Vector = [part.position[0] - parentPosition[0], part.position[1] - parentPosition[1], part.position[2] - parentPosition[2]];
    add(part.id, part.label, drawing, parentId, drawing === 'assembly' || drawing === 'body' ? [0, 0, 0] : position, part.id);
  }
  const engine = items.find(item => item.id === 'engine')!;
  engine.position = [...vehicle.parts.find(part => part.id === 'engine')!.position];
  for (const drawing of ['engine-block', 'engine-head', 'engine-cover'] as const) {
    add(drawing, drawing.replace('engine-', 'Engine '), drawing, 'engine', [0, 0, 0], 'engine');
  }
  addEngineLessonItems(vehicle, add);
  addRunningGear(vehicle, add);
  add('wipers', 'Front windshield wipers', 'assembly', 'body', [0, 0, 0], 'body');
  for (const side of ['left', 'right'] as const) {
    const z = (side === 'left' ? -0.34 : 0.05) * vehicle.dimensions.width;
    add(`wiper-${side}`, `Front ${side} wiper`, 'assembly', 'wipers', [-0.72, 1.19, z], 'body');
    add(`wiper-arm-${side}`, 'Wiper arm', 'wiper-arm', `wiper-${side}`, [0, 0, 0], 'body', 'wiper-arm');
    add(`wiper-blade-${side}`, 'Wiper blade carrier', 'wiper-blade', `wiper-${side}`, [0.08, 0.03, 0.36], 'body', 'wiper-blade');
    add(`wiper-rubber-${side}`, 'Rubber contact edge', 'wiper-rubber', `wiper-blade-${side}`, [0, -0.035, 0], 'body', 'wiper-rubber');
  }
  return validateItems({ items, specifications });
}

function addEngineLessonItems(vehicle: Vehicle, add: AddItem) {
  if (vehicle.id !== 'raize') return;
  const placements = placementsForVehicle(vehicle.id);
  for (const [itemId, drawing] of engineLessonDrawings) {
    const placement = placements.find(candidate => candidate.itemId === itemId);
    if (!placement) throw new Error(`Missing Raize engine placement for ${itemId}.`);
    const specificationId = drawing === 'engine-valve' ? 'engine-valve-study' : itemId;
    const label = drawing === 'engine-valve' ? 'Engine valve' : placement.label;
    add(itemId, label, drawing, 'engine', placement.position, 'engine', specificationId);
  }
}

function addRunningGear(vehicle: Vehicle, add: (
  id: string, label: string, drawing: DrawingKind, parentId: string,
  position: Vector, partId: string, specificationId: string
) => void) {
  const axle = vehicle.dimensions.wheelbase / 2;
  for (const end of [-1, 1]) {
    for (const side of [-1, 1]) {
      const name = `${end < 0 ? 'front' : 'rear'}-${side < 0 ? 'left' : 'right'}`;
      add(`wheel-${name}`, 'Wheel and tire', 'wheel', 'wheels', [end * axle, 0.48, side * vehicle.dimensions.width * 0.49], 'wheels', 'wheel');
      add(`brake-${name}`, 'Brake assembly', 'brake', 'brakes', [end * axle, 0.48, side * vehicle.dimensions.width * 0.39], 'brakes', 'brake');
      add(`suspension-${name}`, 'Suspension strut', 'suspension', 'suspension', [end * axle, 0.8, side * vehicle.dimensions.width * 0.32], 'suspension', 'strut');
    }
  }
}
