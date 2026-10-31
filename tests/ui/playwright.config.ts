import { defineConfig, devices } from '@playwright/test'

// Runs against BASE_URL (the QA web app in CI). Without it, starts the local API and web dev server;
// the local database must be running (`docker compose up -d --wait`).
const baseURL = process.env.BASE_URL ?? 'http://localhost:5173'
const local = !process.env.BASE_URL

const phone = {
  viewport: { width: 393, height: 852 },
  isMobile: true,
  hasTouch: true,
  deviceScaleFactor: 3,
}
const desktop = { viewport: { width: 1440, height: 900 } }

export default defineConfig({
  testDir: './specs',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  // In CI: annotations on the run, the HTML report (uploaded by the QA deploy workflow) and a JSON file for the
  // run summary and the PR comment (.github/scripts/ui_results.py).
  reporter: process.env.CI
    ? [['github'], ['html', { open: 'never' }], ['json', { outputFile: 'results/results.json' }]]
    : [['list'], ['html', { open: 'never' }]],
  use: {
    ...devices['Desktop Chrome'],
    baseURL,
    // Kept only for tests that fail (or pass on the retry): a trace to step through, the screen at the
    // moment of failure and a video of the whole test. They land in test-results/ and in the HTML report.
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    // Optional: use a Chromium already on the machine instead of Playwright's download.
    launchOptions: { executablePath: process.env.CHROMIUM_PATH || undefined },
  },
  // The design's sizes (design/source/CONVENTIONS.md), each in light and dark.
  projects: [
    { name: 'phone-light', use: { ...phone, colorScheme: 'light' } },
    { name: 'phone-dark', use: { ...phone, colorScheme: 'dark' } },
    { name: 'desktop-light', use: { ...desktop, colorScheme: 'light' } },
    { name: 'desktop-dark', use: { ...desktop, colorScheme: 'dark' } },
  ],
  webServer: local
    ? [
        {
          command: 'dotnet run --project ../../api/MyPoGoCompanion.Api --launch-profile http',
          url: 'http://localhost:5080/health/live',
          reuseExistingServer: true,
          timeout: 120_000,
        },
        {
          command: 'npm --prefix ../../web run dev',
          url: baseURL,
          reuseExistingServer: true,
        },
      ]
    : undefined,
})
