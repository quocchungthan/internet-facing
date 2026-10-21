import raize from './raize.json';
import impala from './impala.json';
import engineLessonContent from './engine-lessons.json';
import { parseVehicle } from '../domain/catalog';
import { parseEngineLessons } from '../domain/engineLessons';
import { validateEngineLayoutLessons } from '../features/exploration/domain/engineLayout';

export const vehicles = [parseVehicle(raize), parseVehicle(impala)];
const raizeVehicle = vehicles.find(vehicle => vehicle.id === 'raize');
if (!raizeVehicle) throw new Error('The Raize lesson source catalog is unavailable.');

export const engineLessons = parseEngineLessons(engineLessonContent, raizeVehicle.sources);
validateEngineLayoutLessons(engineLessons);
