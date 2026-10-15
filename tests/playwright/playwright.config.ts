import { defineConfig, devices } from '@playwright/test';

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? 'https://dangkyphep.fcc-vn.com.vn';

export default defineConfig({
  testDir: './specs',
  outputDir: 'test-results',
  fullyParallel: true,
  forbidOnly: Boolean(process.env.CI),
  retries: process.env.CI ? 1 : 0,
  reporter: [
    ['list'],
    ['html', { outputFolder: 'playwright-report', open: 'never', theme: 'light' }],
    ['json', { outputFile: 'test-results/results.json' }],
  ],
  use: {
    baseURL,
    ignoreHTTPSErrors: true,
    // Long crawls produce huge traces/videos; keep them off on CI (set PLAYWRIGHT_TRACE=on to debug).
    trace: process.env.PLAYWRIGHT_TRACE === 'on' ? 'retain-on-failure' : 'off',
    screenshot: 'only-on-failure',
    video: 'off',
    actionTimeout: 15_000,
    navigationTimeout: 45_000,
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 } },
    },
  ],
});
