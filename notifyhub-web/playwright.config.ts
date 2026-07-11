import { defineConfig, devices } from "@playwright/test";

// Prerequisite: the full docker-compose stack (mysql, api, worker, web) must already be
// running (`docker-compose up -d` from the repo root) before running this suite.
// Playwright can only launch one process via `webServer`, but this app needs the whole
// stack — DB, background worker, SignalR hub — to behave correctly, so there's no
// webServer auto-start block here; `npm run test:e2e` assumes it's already up.
export default defineConfig({
  testDir: "./e2e",
  retries: 0,
  reporter: "list",
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? "http://localhost:5173",
    trace: "retain-on-failure",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
