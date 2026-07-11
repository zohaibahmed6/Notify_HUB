import { expect, test } from "@playwright/test";

import { SEED_USERS, loginViaUi } from "./helpers";

test("logs in as Staff", async ({ page }) => {
  await loginViaUi(page, SEED_USERS.staff);
  await expect(page.getByText(`${SEED_USERS.staff.username} (Staff)`)).toBeVisible();
});

test("logs in as Admin", async ({ page }) => {
  await loginViaUi(page, SEED_USERS.admin);
  await expect(page.getByText(`${SEED_USERS.admin.username} (Admin)`)).toBeVisible();
});

// Bug fix proof: session used to be lost on any reload because the refresh token was
// only ever held in-memory. It now lives in an httpOnly cookie, so a silent
// POST /api/auth/refresh on app bootstrap should restore the session without a redirect
// to /login.
test("page refresh keeps the session alive", async ({ page }) => {
  await loginViaUi(page, SEED_USERS.staff);

  await page.reload();

  await expect(page).toHaveURL(/\/inbox$/);
  await expect(page.getByText(`${SEED_USERS.staff.username} (Staff)`)).toBeVisible();
});

test("sign out actually ends the session (no silent restore after reload)", async ({ page }) => {
  await loginViaUi(page, SEED_USERS.staff);

  await page.getByRole("button", { name: "Sign out" }).click();
  await page.waitForURL("**/login");

  await page.reload();
  await expect(page).toHaveURL(/\/login$/);
});
