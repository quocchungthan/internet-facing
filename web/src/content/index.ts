import raize from './raize.json';
import impala from './impala.json';
import { parseVehicle } from '../domain/catalog';

export const vehicles = [parseVehicle(raize), parseVehicle(impala)];
