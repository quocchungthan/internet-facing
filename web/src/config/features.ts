import { readFeatureFlags } from './featureFlags';

export const features = readFeatureFlags(import.meta.env);
