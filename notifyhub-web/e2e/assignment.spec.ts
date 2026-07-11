import { expect, test } from "@playwright/test";

import { API_BASE_URL, SEED_USERS, apiLogin, findThreadId, loginViaUi, seededPatient, sendInboundWebhook } from "./helpers";

const PATIENT = seededPatient(3);

// BR-012: assignment is monotonic (once assigned, stays assigned), so this test checks
// current state before acting rather than assuming a pristine thread — safe to re-run
// against the same persistent dev DB.
test("thread assignment, then a concurrent assign attempt returns 409", async ({ page, request }) => {
  await sendInboundWebhook(request, PATIENT.phone, "Do you have anything earlier in the day?");

  await loginViaUi(page, SEED_USERS.staff);
  await page.getByRole("button", { name: new RegExp(PATIENT.name) }).click();

  // "Make task" always renders once the panel finishes loading, regardless of
  // assignment state — wait for it before reading the conditionally-rendered "Assign to
  // me" button, otherwise isVisible() can race the initial thread-detail fetch and
  // report false simply because the panel hasn't rendered yet, not because it's
  // genuinely already assigned.
  await expect(page.getByRole("button", { name: "Make task" })).toBeVisible();

  const assignButton = page.getByRole("button", { name: "Assign to me" });
  if (await assignButton.isVisible()) {
    await assignButton.click();
    await expect(page.getByText("Thread assigned")).toBeVisible();
  }
  // Scoped by absence of the button rather than "Assigned to staff" text: the sidebar
  // lists every thread's own assignee, and "staff" is a substring of "staff2", so a
  // page-wide text search is ambiguous the moment more than one thread is assigned.
  await expect(assignButton).toHaveCount(0);

  // The thread is now guaranteed assigned (this run or a prior one) — a second staff
  // member attempting to assign it must be rejected.
  const { token: staff2Token } = await apiLogin(request, SEED_USERS.staff2);
  const threadId = await findThreadId(request, staff2Token, PATIENT.name);
  const response = await request.post(`${API_BASE_URL}/api/threads/${threadId}/assign`, {
    headers: { Authorization: `Bearer ${staff2Token}` },
    data: {},
  });

  expect(response.status()).toBe(409);
});
