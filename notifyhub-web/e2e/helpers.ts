import type { APIRequestContext, Page } from "@playwright/test";

// The frontend runs on 5173 (playwright.config.ts baseURL); API calls used for test
// fixtures (creating threads/messages, escalating tasks) go straight to the backend.
export const API_BASE_URL = process.env.NOTIFYHUB_API_URL ?? "http://localhost:5000";

// Matches the dev values in the repo root .env (SEED__*), not secrets worth hiding.
export const SEED_USERS = {
  staff: { username: "staff", password: "StaffDev1!" },
  staff2: { username: "staff2", password: "Staff2Dev1!" },
  admin: { username: "admin", password: "AdminDev1!" },
} as const;

// Must match the repo root .env's WEBHOOKS__SHAREDSECRET.
const WEBHOOK_SECRET = process.env.NOTIFYHUB_WEBHOOK_SECRET ?? "dev-webhook-shared-secret-change-me";

/** Seeded synthetic patients (PatientAppointmentSeedStep): "Patient 01".."Patient 10", phones +155501000NN. */
export function seededPatient(index: number) {
  const suffix = index.toString().padStart(3, "0");
  return { name: `Patient ${index.toString().padStart(2, "0")}`, phone: `+15550100${suffix}` };
}

/** Logs in via the API and returns a bearer token + user id, for building test fixtures without driving the UI. */
export async function apiLogin(request: APIRequestContext, user: { username: string; password: string }) {
  const response = await request.post(`${API_BASE_URL}/api/auth/login`, { data: user });
  if (!response.ok()) {
    throw new Error(`apiLogin failed for ${user.username}: ${response.status()}`);
  }
  const body = await response.json();
  return { token: body.accessToken as string, userId: body.user.id as number };
}

/** Cancels any pre-existing Escalated tasks assigned to the given user — a previous test
 * run that failed before reaching the UI "open" step (which triggers BR-014's revert)
 * can leave one orphaned, which would otherwise make row-matching by assignee ambiguous. */
export async function cancelStaleEscalatedTasks(request: APIRequestContext, token: string, assignedStaffId: number) {
  const response = await request.get(
    `${API_BASE_URL}/api/tasks?status=Escalated&assignedStaffId=${assignedStaffId}&pageSize=100`,
    { headers: { Authorization: `Bearer ${token}` } },
  );
  const body = await response.json();
  for (const task of body.items as { id: number }[]) {
    await request.patch(`${API_BASE_URL}/api/tasks/${task.id}`, {
      headers: { Authorization: `Bearer ${token}` },
      data: { status: "Cancelled" },
    });
  }
}

/** Simulates an inbound patient SMS via the mock gateway webhook (creates the thread if it doesn't exist yet). */
export async function sendInboundWebhook(request: APIRequestContext, phone: string, body: string) {
  const response = await request.post(`${API_BASE_URL}/api/webhooks/inbound`, {
    headers: { "X-Webhook-Secret": WEBHOOK_SECRET },
    data: { phone, body },
  });
  if (!response.ok()) {
    throw new Error(`sendInboundWebhook failed: ${response.status()}`);
  }
}

/** Finds a thread's id by the patient's seeded display name (e.g. "Patient 03"). */
export async function findThreadId(request: APIRequestContext, token: string, patientName: string) {
  const response = await request.get(`${API_BASE_URL}/api/threads?pageSize=100`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  const body = await response.json();
  const thread = body.items.find((t: { patientName: string }) => t.patientName === patientName);
  if (!thread) {
    throw new Error(`No thread found for ${patientName} — did the inbound webhook fire first?`);
  }
  return thread.id as number;
}

export async function loginViaUi(page: Page, user: { username: string; password: string }) {
  await page.goto("/login");
  await page.getByLabel("Username").fill(user.username);
  await page.getByLabel("Password").fill(user.password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await page.waitForURL("**/inbox");
}
