import { expect, test } from "@playwright/test";

import { SEED_USERS, loginViaUi, seededPatient, sendInboundWebhook } from "./helpers";

// Patient 02 was already opted-out from unrelated earlier manual testing (pre-existing
// dev DB state) — picked 08 instead since patients 8-10 were otherwise untouched.
const PATIENT = seededPatient(8);

// SignalR proof: two independent browser contexts (distinct staff identities), both with
// the same thread open. A reply sent in one must appear in the other's conversation
// panel with no reload — that's only possible via the InboxHub push, not a query refetch
// triggered by the sender's own action.
test("a reply sent by one staff member appears live for another, with no reload", async ({ browser, request }) => {
  await sendInboundWebhook(request, PATIENT.phone, "Can I reschedule my visit?");

  const contextA = await browser.newContext();
  const contextB = await browser.newContext();
  const pageA = await contextA.newPage();
  const pageB = await contextB.newPage();

  try {
    await loginViaUi(pageA, SEED_USERS.staff);
    await loginViaUi(pageB, SEED_USERS.staff2);

    await pageA.getByRole("button", { name: new RegExp(PATIENT.name) }).click();
    await pageB.getByRole("button", { name: new RegExp(PATIENT.name) }).click();

    // Unique per run: re-running against the same persistent dev DB accumulates
    // messages in this thread, and an identical draft on a prior successful run would
    // otherwise match more than one element here.
    const draft = `Of course — what date works for you? (${Date.now()})`;
    await pageA.getByPlaceholder("Type a reply...").fill(draft);
    await pageA.getByRole("button", { name: "Send" }).click();
    await expect(pageA.getByText("Message sent")).toBeVisible();

    // No reload/navigation on pageB — this only passes if the SignalR push landed.
    await expect(pageB.getByText(draft)).toBeVisible({ timeout: 10_000 });
  } finally {
    await contextA.close();
    await contextB.close();
  }
});
