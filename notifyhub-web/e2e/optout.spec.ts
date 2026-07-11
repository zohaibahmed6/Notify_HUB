import { expect, test } from "@playwright/test";

import { SEED_USERS, loginViaUi, seededPatient, sendInboundWebhook } from "./helpers";

const PATIENT = seededPatient(4);

// BR-001b: opted-out patients must be blocked from further outbound sends, including
// staff ad-hoc replies — the UI disables the reply box entirely rather than letting the
// send fail server-side. The webhook's opt-out is idempotent (only flips on first STOP),
// so re-running this test is safe.
test("an opted-out patient's reply box is disabled", async ({ page, request }) => {
  await sendInboundWebhook(request, PATIENT.phone, "STOP");

  await loginViaUi(page, SEED_USERS.staff);
  await page.getByRole("button", { name: new RegExp(PATIENT.name) }).click();

  await expect(page.getByText("Patient opted out")).toBeVisible();
  await expect(page.getByPlaceholder("Patient has opted out")).toBeDisabled();
  await expect(page.getByRole("button", { name: "Send" })).toBeDisabled();
});
