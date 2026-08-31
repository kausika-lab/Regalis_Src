import { defineConfig } from '@playwright/test';

/**
 * End-to-end walkthrough of the running app (API + Vite dev server).
 * Launch everything with scripts\run-e2e.ps1 — it starts whatever is not
 * already running, resolves the admin password from user-secrets, and then
 * runs this suite. E2E specs live in e2e/ and are excluded from vitest.
 */
export default defineConfig({
  testDir: './e2e',
  timeout: 90_000,
  workers: 1,
  retries: 0,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'e2e-report' }]],
  outputDir: 'e2e-results',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:5173',
    screenshot: 'only-on-failure',
    trace: 'retain-on-failure',
    viewport: { width: 1440, height: 900 },
    // E2E_SLOWMO=<ms> pauses between every browser action so the headed
    // walkthrough is watchable (e.g. 500). Unset = full speed.
    launchOptions: process.env.E2E_SLOWMO
      ? { slowMo: Number(process.env.E2E_SLOWMO) }
      : {},
  },
});
