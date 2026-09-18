import { defineConfig, devices } from "@playwright/test";

// Port 3100 rather than Next's default 3000 so e2e never attaches to an
// unrelated dev server a developer already has running.
const E2E_PORT = 3100;
const baseURL = process.env.E2E_BASE_URL ?? `http://localhost:${E2E_PORT}`;

export default defineConfig({
  testDir: "./specs",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,

  /*
   * One worker locally, Playwright's own default in CI.
   *
   * Playwright defaults to half the machine's cores, which on a developer box already
   * running the API, a dev server and a browser meant a different handful of tests failed
   * every run — always `page.goto` exceeding its timeout, never the same feature twice.
   * The app was not at fault: it answers the same requests in milliseconds when the
   * machine is not saturated, and the failure rate tracked the worker count rather than
   * anything in the code (1 worker: all 77 passing, twice over; 2 workers: 63; 4 workers:
   * between 56 and 70, varying run to run).
   *
   * A suite that fails differently every time is worse than no suite, because people stop
   * reading it. The whole run takes about two minutes serially, which is a cheap price for
   * a result that means something. Override with `--workers=N` when the machine is idle.
   */
  workers: process.env.CI ? undefined : 1,
  reporter: process.env.CI ? [["html"], ["list"]] : "list",
  use: {
    baseURL,
    trace: "on-first-retry",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
  ],
  webServer: process.env.E2E_BASE_URL
    ? undefined
    : {
        command: `npm run dev -- --port ${E2E_PORT}`,
        cwd: "../../src/web/gosit-web",
        url: baseURL,
        reuseExistingServer: false,
        timeout: 120_000,
      },
});
