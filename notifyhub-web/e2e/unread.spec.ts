import { expect, test } from "@playwright/test";

import { SEED_USERS, loginViaUi, seededPatient, sendInboundWebhook } from "./helpers";

const PATIENT = seededPatient(5);

// §6c: unread count resets to 0 the moment the thread is opened (ThreadsController.Detail).
test("opening a thread resets its unread badge", async ({ page, request }) => {
  await sendInboundWebhook(request, PATIENT.phone, "Just confirming my appointment time.");

  await loginViaUi(page, SEED_USERS.staff);

  const threadButton = page.getByRole("button", { name: new RegExp(PATIENT.name) });
  const unreadBadge = threadButton.locator("div").filter({ hasText: /^\d+$/ });
  await expect(unreadBadge).toBeVisible();

  await threadButton.click();

  await expect(unreadBadge).toHaveCount(0);
});
