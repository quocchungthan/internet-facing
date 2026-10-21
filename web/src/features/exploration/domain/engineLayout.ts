import { engineLessonItemIds, type EngineLesson } from '../../../domain/engineLessons';
import type { Vector } from './items';

export type EnginePlacement = { label: string; position: Vector };

const cylinderZSpacing = 0.21 * 0.72;
const cylinderY = 0.24 * 0.72;
const representativeCylinderZ = cylinderZSpacing;

const placements: Record<typeof engineLessonItemIds[number], EnginePlacement> = {
  'engine-cylinder-study': { label: 'Cylinder wall / bore', position: [0, 0, representativeCylinderZ] },
  'engine-piston-study': { label: 'Piston', position: [0, 0, representativeCylinderZ] },
  'engine-rod-study': { label: 'Connecting rod', position: [0, -0.13, representativeCylinderZ] },
  'engine-crankshaft-study': { label: 'Crankshaft', position: [0, -0.22, representativeCylinderZ] },
  'engine-intake-valve-study': { label: 'Intake valve', position: [-0.055, 0.15, representativeCylinderZ] },
  'engine-exhaust-valve-study': { label: 'Exhaust valve', position: [0.055, 0.15, representativeCylinderZ] },
  'engine-spark-plug-study': { label: 'Spark plug', position: [0, 0.25, representativeCylinderZ] },
};

function copyPosition([x, y, z]: Vector): Vector {
  return [x, y, z];
}

export const engineLayout = {
  cylinderAxis: 'y',
  crankshaftAxis: 'z',
  representativeCylinder: 'engine-cylinder-study',
  contextCylinderCenters: [
    [0, cylinderY, -cylinderZSpacing],
    [0, cylinderY, 0],
    [0, cylinderY, cylinderZSpacing],
  ] satisfies Vector[],
};

export function validateEngineLayoutLessons(lessons: readonly Pick<EngineLesson, 'carId' | 'itemId'>[]) {
  const raizeIds = lessons.filter(lesson => lesson.carId === 'raize').map(lesson => lesson.itemId);
  const uniqueIds = new Set(raizeIds);
  if (uniqueIds.size !== engineLessonItemIds.length
    || raizeIds.length !== engineLessonItemIds.length
    || engineLessonItemIds.some(itemId => !uniqueIds.has(itemId))) {
    throw new Error('Engine placements must match the seven Raize lesson identities.');
  }
}

export function placementsForVehicle(vehicleId: string) {
  if (vehicleId !== 'raize') return [];
  return engineLessonItemIds.map(itemId => ({
    itemId,
    label: placements[itemId].label,
    position: copyPosition(placements[itemId].position),
  }));
}
