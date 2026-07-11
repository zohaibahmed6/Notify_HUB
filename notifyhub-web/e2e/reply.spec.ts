import { expect, test } from "@playwright/test";

import { SEED_USERS, loginViaUi, seededPatient, sendInboundWebhook } from "./helpers";

const PATIENT = seededPatient(1);

// Bug fix proof: Reply used to return 200 with an empty body, which apiClient's
// response.json() call threw a SyntaxError on — surfacing as "Message failed to send"
// even though the message saved successfully. Reply now returns 204, which apiClient
// already special-cases, so a real success should show the real success toast.
test("sending a reply shows success feedback matching the actual result", async ({ page, request }) => {
  await sendInboundWebhook(request, PATIENT.phone, "Hi, I have a question about my appointment.");

  await loginViaUi(page, SEED_USERS.staff);
  await page.getByRole("button", { name: new RegExp(PATIENT.name) }).click();

  // Unique per run: re-running against the same persistent dev DB accumulates messages
  // in this thread, and an identical draft on a prior successful run would otherwise
  // match more than one element here.
  const draft = `Sure, happy to help — what's your question? (${Date.now()})`;
  await page.getByPlaceholder("Type a reply...").fill(draft);
  await page.getByRole("button", { name: "Send" }).click();

  await expect(page.getByText("Message sent")).toBeVisible();
  await expect(page.getByText("Message failed to send")).not.toBeVisible();
  await expect(page.getByText(draft)).toBeVisible();
});
