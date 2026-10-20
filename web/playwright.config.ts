import { defineConfig } from '@playwright/test';

const externalServer = process.env.PLAYWRIGHT_BASE_URL;

export default defineConfig({
  testDir: './tests',
  timeout: 60000,
  expect: { timeout: 15000, toHaveScreenshot: { maxDiffPixelRatio: 0.001, animations: 'disabled' } },
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  use: {
    baseURL: externalServer ?? 'http://127.0.0.1:4173',
    headless: true,
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    launchOptions: { args: ['--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] },
  },
  webServer: externalServer ? undefined : {
    command: 'npm run preview -- --port 4173 --strictPort',
    url: 'http://127.0.0.1:4173',
    reuseExistingServer: !process.env.CI,
    timeout: 30000,
  },
});
