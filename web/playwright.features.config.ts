import { defineConfig } from '@playwright/test';

export default defineConfig({
  testDir: './tests',
  testMatch: 'featureFlags.spec.ts',
  outputDir: 'test-results/features',
  timeout: 120000,
  expect: { timeout: 15000 },
  workers: 1,
  retries: 0,
  use: {
    headless: true,
    reducedMotion: 'reduce',
    viewport: { width: 1440, height: 900 },
    trace: 'retain-on-failure',
    launchOptions: { args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] },
  },
});
