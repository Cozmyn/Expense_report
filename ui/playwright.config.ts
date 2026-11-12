import { defineConfig } from '@playwright/test';
// Real external journeys are destructive; never point this suite at Production.
const externalOrigin = process.env.DECONT_E2E_BASE_URL;
if (externalOrigin) {
  const address = new URL(externalOrigin);
  if (address.protocol !== 'https:' ||
      !/(?:^|[.-])(uat|staging|test)(?:[.-]|$)/i.test(address.hostname) ||
      address.pathname !== '/' || address.search || address.hash) {
    throw new Error('External browser workflows require an isolated HTTPS UAT hostname');
  }
}
export default defineConfig({
  testDir: './e2e',
  workers: 1,
  retries: 0,
  timeout: 180000,
  expect: { timeout: 15000 },
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    actionTimeout: 15000,
    baseURL: externalOrigin || 'http://127.0.0.1:5080',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: [
    { name: 'chromium', use: { browserName: 'chromium' } },
    {
      name: 'mobile',
      use: {
        browserName: 'chromium',
        viewport: { width: 390, height: 844 },
        isMobile: true,
        hasTouch: true,
      },
    },
  ],
});
