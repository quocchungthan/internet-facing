import { loadEnv } from 'vite';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { readFeatureFlags } from './src/config/featureFlags';

export default defineConfig(({ mode }) => {
  readFeatureFlags(loadEnv(mode, process.cwd(), 'VITE_FEATURE_'));
  return {
    plugins: [react()],
    test: { include: ['src/**/*.test.ts'] },
  };
});
