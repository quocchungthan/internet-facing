import { loadEnv } from 'vite';
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { readFeatureFlags } from './src/config/featureFlags';
import { publisherLocation } from './src/config/publisher';

export default defineConfig(({ mode }) => {
  readFeatureFlags(loadEnv(mode, process.cwd(), 'VITE_FEATURE_'));
  return {
    plugins: [
      react(),
      {
        name: 'publisher-city-metadata',
        transformIndexHtml: () => [{
          tag: 'meta',
          attrs: { name: 'geo.placename', content: `${publisherLocation.city}, ${publisherLocation.countryName}` },
          injectTo: 'head' as const,
        }],
      },
    ],
    test: { include: ['src/**/*.test.ts'] },
  };
});
