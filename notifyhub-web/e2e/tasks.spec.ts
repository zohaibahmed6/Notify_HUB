import { expect, test } from "@playwright/test";

import {
  API_BASE_URL,
  SEED_USERS,
  apiLogin,
  cancelStaleEscalatedTasks,
  findThreadId,
  loginViaUi,
  seededPatient,
  sendInboundWebhook,
} from "./helpers";

// FR-008: system-suggested due date by priority (TaskDueDateDefaults), staff-overridable.
// Leaving "Due" blank in the form must produce the backend's computed default — assert
// against the actual network response, not the list UI (which has no per-task identity
// to search by beyond due-date text).
test("creating a task with no due date applies the priority default", async ({ page, request }) => {
  const patient = seededPatient(6);
  await sendInboundWebhook(request, patient.phone, "Need a follow-up call about my results.");

  await loginViaUi(page, SEED_USERS.staff);
  await page.getByRole("button", { name: new RegExp(patient.name) }).click();

  await page.getByRole("button", { name: "Make task" }).click();
  await page.locator("#task-priority").selectOption("Urgent");

  const beforeCreate = Date.now();
  const [response] = await Promise.all([
    page.waitForResponse((res) => res.url().includes("/tasks") && res.request().method() === "POST"),
    page.getByRole("button", { name: "Create task" }).click(),
  ]);

  await expect(page.getByText("Task created")).toBeVisible();

  const created = await response.json();
  const dueAt = new Date(created.dueAt).getTime();
  const expectedDueAt = beforeCreate + 4 * 60 * 60 * 1000; // Urgent -> +4h (TaskDueDateDefaults)

  // Generous tolerance for test/network latency, not for correctness of the rule itself.
  expect(Math.abs(dueAt - expectedDueAt)).toBeLessThan(5 * 60 * 1000);
});

// BR-014 proof: opening an escalated task (GET /api/tasks/{id}) must auto-revert it to
// in_progress when the caller is the assignee. Setup goes through the API directly
// (PATCH to Escalated) since the 60s-poll background worker is too slow for a test.
// Once opened, the task leaves the Escalated filter for good (reverted), so a normal run
// self-cleans — but a run that failed before reaching that step (e.g. mid-development)
// can leave an orphaned Escalated task behind, which would make row-matching by assignee
// ambiguous. Clearing those first makes the test robust regardless of prior failures.
test("opening an escalated task from the board reverts it to in_progress", async ({ page, request }) => {
  const patient = seededPatient(7);
  await sendInboundWebhook(request, patient.phone, "When is my next visit?");

  const { token: staff2Token, userId: staff2Id } = await apiLogin(request, SEED_USERS.staff2);
  await cancelStaleEscalatedTasks(request, staff2Token, staff2Id);
  const threadId = await findThreadId(request, staff2Token, patient.name);

  const createResponse = await request.post(`${API_BASE_URL}/api/threads/${threadId}/tasks`, {
    headers: { Authorization: `Bearer ${staff2Token}` },
    data: { priority: "Medium" },
  });
  const task = await createResponse.json();

  const escalateResponse = await request.patch(`${API_BASE_URL}/api/tasks/${task.id}`, {
    headers: { Authorization: `Bearer ${staff2Token}` },
    data: { status: "Escalated" },
  });
  expect(escalateResponse.ok()).toBeTruthy();

  await loginViaUi(page, SEED_USERS.staff2);
  await page.getByRole("link", { name: "Task board" }).click();
  await page.getByRole("button", { name: "Escalated" }).click();

  const row = page.getByRole("button").filter({ hasText: SEED_USERS.staff2.username });

  // Clicking the row is the real "open" action under test — it fires GET
  // /api/tasks/{id}, which is what triggers the backend's BR-014 revert. Asserting on
  // that response directly (rather than on the list DOM) sidesteps a real UX side
  // effect: once reverted, the task immediately drops out of the still-active
  // "Escalated" filter, so the row/detail panel can unmount before a DOM assertion
  // would see it.
  const [detailResponse] = await Promise.all([
    page.waitForResponse((res) => res.url().includes(`/api/tasks/${task.id}`) && res.request().method() === "GET"),
    row.click(),
  ]);

  const detail = await detailResponse.json();
  expect(detail.status).toBe("InProgress");
});
