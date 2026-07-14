# NotifyHub — Build Status

## Plan numbering
This file previously used a self-inferred 6-step breakdown that combined skeleton+auth into one step. Corrected against your actual plan (confirmed 2026-07-11): **1**=skeleton, **2**=auth, **3**=outbound pipeline (FR-001–004), **4**=inbound routing + task engine (FR-005–008), **5**=reminders (FR-009), **6**=audit + 50k seed (FR-010/011), **7**=docs/ADR/OWASP/AI-log (FR-012–019/FR-016–019). What this file previously labeled "Step 2" was actually step 3, and "Step 3" was actually step 4 — relabeled below; git commit messages from before this correction still say "step 2" and are left as-is (historical record, not rewritten).

## Current step
Step 9 (2026-07-14, in progress) — locked spec in `STEP9_PLAN.md` (repo root), a further
informally-specified increment list from a 2026-07-14 planning session with Zohaib (P9-00
responsive design + P9-01 through P9-12). Building in the file's stated order, one
independently-committed increment at a time. P9-00 (responsive design pass) done — see
"Step 9 checklist" below; P9-01 onward in progress.

## Step 9 checklist
- [x] **P9-00 — Responsive design pass** on the v2 screens (`AppShell`, `InboxPageV2`,
      `TemplatesPageV2`, `AuditLogPageV2`, `TaskDetailSheet`, `DashboardPage`,
      `SettingsPage`'s tab list) — see `CODEBASE_MAP.md` §6e for the per-file breakdown.
      `tsc -b`/`vite build` clean; `docker compose up -d --build web` verified the container
      serves the rebuilt bundle. No browser/screenshot tool was available this session, so
      this was **not** visually click-through-verified in a real viewport like step 8's
      screens were — verified via type-check/build and reading the applied Tailwind
      breakpoints instead. Flagging this as a real gap: a follow-up visual pass (resize the
      browser at each of `AppShell`/Inbox/Templates/Audit log/Task board/Task detail
      sheet/Dashboard/Settings) is recommended before treating P9-00 as fully done.
- [x] **Pre-existing e2e staleness found (not caused by P9-00)**: the whole Playwright suite
      fails at `loginViaUi` (`e2e/helpers.ts:79`, `page.waitForURL("**/inbox")`) because
      `LoginPage.tsx`/`LoginPageV2.tsx` have `navigate("/")` on success, and `/` has rendered
      `DashboardPage` instead of redirecting to `/inbox` since increment 13 (step 8) —
      `helpers.ts` was never updated for that change. Confirmed by reading both login pages'
      `navigate()` calls; not something this session touched. Left as-is (fixing the e2e
      suite isn't in `STEP9_PLAN.md`'s scope) but flagged here so it isn't mistaken for a
      P9-00 regression next time the suite is run.
- [x] P9-01 — quick fixes (command palette removal, Forward dialog exclusion, sheet
      auto-close, task form date/time split, remove `OffsetHours` from Templates UI) — see
      `CODEBASE_MAP.md` §6f. Flagged (not silently fixed): `STEP9_PLAN.md` attributes the
      Reminder Offset settings to "P9-10" but they're actually defined by P9-08's rules 6/16 —
      treated as a typo, P9-08 is where those Settings → SMS fields will actually be built.
      `tsc -b`/`vite build` clean, `docker compose up -d --build web` verified serving. No
      backend changes (no schema touched, matching the increment's own title).
- [x] P9-02 — SignalR delivery-status double-tick fix — see `CODEBASE_MAP.md` §3's new
      `messageStatusUpdated` row. 160/160 fast backend tests still passing; verified live
      end-to-end against the real Docker stack with a scripted SignalR client (a JWT'd
      connection to `/hubs/inbox` listening for the event while a real reply was sent via
      `curl`) rather than just reading the code — confirmed `outboundMessageSent` then
      `messageStatusUpdated {threadId, messageId, status:"Delivered"}` both arrive.
- [x] P9-03 — shared `DateTimePicker` — see `CODEBASE_MAP.md` §6g. `tsc -b`/`vite build`
      clean, `docker compose up -d --build web` verified serving. **Not** click-through
      verified in a real browser (no browser tool available this session) — the clock
      face's pointer-angle math is the highest-risk unverified piece in Step 9 so far;
      recommend an actual manual pass before relying on it.
- [x] P9-04 — template bookmark resolution in composer — new `GET
      api/threads/{id}/templates/{templateId}/preview` endpoint (`ThreadsController`),
      `ConversationPanelV2.handleInsertTemplate` now calls it instead of inserting the
      raw template body. See `CODEBASE_MAP.md` §3. 2 new integration tests, 84/84 fast
      backend tests passing. Verified live against the real Docker stack via curl (real
      patient name + real upcoming appointment both resolved correctly).
- [x] P9-05 — template edits propagate to queued messages — see `CODEBASE_MAP.md`'s
      `TemplatesController` row. Verified (not assumed) that dispatch-time re-render
      already fully covers propagation today; the explicit save-time sweep is kept as
      the dual-safety net anyway, per Zohaib's explicit call. 85/85 fast backend tests
      passing (1 new). Verified live against the real Docker/MySQL stack via `curl` (the
      real-provider `ExecuteUpdateAsync` branch, which InMemory tests can't exercise) —
      restored the touched seed template's body afterward.
- [ ] P9-06 — SMS History report
- [ ] P9-07 — message expiry engine
- [ ] P9-08 — reminder SMS engine (retires `ReminderScheduler`/`ReminderWorker`)
- [ ] P9-09 — PDU segment count
- [ ] P9-10 — task forwarding rules
- [ ] P9-11 — recurring task configuration UI
- [ ] P9-12 — user on-leave From/To dates

## Current step (historical, step 7)
Step 7 of 7 (docs) — README, 3 ADRs, OWASP self-assessment, AI usage log, FR-013 coverage number. Step 6 was reviewed and confirmed working (audit log correct for both roles, pagination/filters work, 50k seed correctly skips the live dispatcher) and is committed (`1c6c47b`). See "Step 7 checklist" below.

## Step 4 bug-fix + e2e round
Live testing surfaced 3 bugs; a Playwright suite was built to cover the fixes and lock them in. Full details in the change log, summarized here:
- **Reply/assign showed "failed" despite succeeding** — `Reply`/`Assign` returned `200 OK` with an empty body; `apiClient`'s `response.json()` threw a `SyntaxError` on the empty body, which wasn't an `ApiError` so it fell through to the generic failure toast. Fixed: both now return `204 No Content`, which `apiClient` already special-cased.
- **Page refresh forced re-login** — a real conflict in the original spec: §6a said tokens must be in-memory only ("not localStorage, to reduce XSS exposure"), but in-memory state is wiped by any reload, so nothing survived to restore a session from. Fixed (your call, httpOnly cookie option): the refresh token now lives in an httpOnly cookie the browser sends automatically — JS can never read it, stronger than the original in-memory approach — and `AuthContext` calls `/api/auth/refresh` silently on mount to restore the session. Added `POST /api/auth/logout` so signing out actually revokes the cookie server-side (otherwise it would've silently un-done a manual sign-out on the next reload). §6a/§8 in PROJECT_CONTEXT.md updated to match.
- **Opening an escalated task didn't revert it to in_progress** — the backend's BR-014 logic (`TasksController.Detail`) was already correct, but the frontend had no "open task" UI at all — no click handler, no detail view, so that endpoint was never called. Fixed: Task board rows are now clickable, expanding an inline detail panel backed by a new `useTask(id)` hook, which fires the `GET` that triggers the revert.
- **Found by the e2e suite itself, not in the original bug list**: staff replies never broadcast over SignalR at all (only thread assignment did) — a reply sent in one tab never appeared live in another open tab on the same thread. Fixed by adding an `outboundMessageSent` broadcast from `ThreadsController.Reply`, mirroring the existing `threadAssigned` one, and a matching listener in `useInboxHub`.
- **Test-infra fixes required to make the suite runnable at all** (not app bugs, but worth knowing about): Vite's dev server rejects requests with an unrecognized `Host` header by default (`server.allowedHosts: true` added); the frontend's API base URL was baked in as a fixed `http://localhost:5000` at dev-server-start time, which only resolves correctly when the browser and API share the literal string "localhost" — replaced with a runtime derivation from the page's own hostname (`src/lib/apiBaseUrl.ts`), and the committed `notifyhub-web/.env.development` override that pinned the old fixed value was removed; CORS now accepts a comma-separated list of allowed origins (`Cors:WebOrigin`) instead of just one, so the same running stack can serve both a developer's own browser and the e2e suite.

## Playwright e2e suite (new)
`notifyhub-web/e2e/` — 11 tests across 7 spec files, **11/11 passing**. Covers: login (Staff + Admin) and page-refresh session persistence (bug #2 proof), reply success feedback matching the actual result (bug #1 proof), a real two-browser-context SignalR test (reply in one tab, appears live in the other — bug #4 proof), thread assignment + the 409-on-double-assign race, an opted-out patient's disabled reply box, unread-count reset on thread open, task creation with correct priority/due-date defaults, and the escalated→in_progress revert on task open (bug #3 proof).

**How to run**: requires the full `docker-compose` stack already running (`docker-compose up -d`) — Playwright can only launch one process via its own `webServer` option, but this app needs the whole stack (DB, background worker, SignalR) to behave correctly, so there's no auto-start. No local `node`/`npm` were available in this build environment, so the suite was installed and run via `docker run` against the official `mcr.microsoft.com/playwright` image (version must match `@playwright/test` in `package.json`, currently pinned to `1.61.1`), with `PLAYWRIGHT_BASE_URL`/`NOTIFYHUB_API_URL` pointed at `http://host.docker.internal:5173`/`:5000` so the containerized browser reaches the compose stack's published ports (chosen over attaching to the compose network directly and using service DNS names, since the browser's `Origin` needs to share a hostname with the API for the refresh cookie's `SameSite=Lax` to actually be sent). From a machine with `node`/`npm`, it's just `cd notifyhub-web && npm run test:e2e`.
Test fixtures use Playwright's `request` fixture to hit the API directly (create threads via the inbound webhook, escalate a task via `PATCH`) rather than driving the UI for every precondition. Each spec uses a distinct seeded patient (01/03/04/05/06/07/08) to avoid cross-test collisions; patient 02 was skipped after discovering it was already opted-out from unrelated earlier manual testing.

## Step 3 checklist (reviewed) — was labeled "Step 2"
- [x] Reviewed by Zohaib — real dispatcher confirmed processing messages end-to-end in Docker (queue→render→gateway→status update, 10/10 succeeded), Swagger Authorize works, fresh-volume startup works, restart doesn't duplicate seed data
- Note: the first review round caught a stale Docker image (`docker-compose up` without `--build` had reused an image predating the real-`DispatcherWorker` commit) — not a code/wiring bug, fixed by rebuilding; see git log `d5f378d`.

## Steps 1–2 checklist (reviewed) — were combined and labeled "Step 1"
- [x] Solution skeleton, EF Core + MySQL, JWT auth (login/refresh/RBAC), seed scaffolding, Docker/compose, CI, React login screen
- [x] Reviewed by Zohaib — Docker stack, Admin + Staff login, JWT role claims all verified working
- [x] Swagger JWT bearer auth wired (`AddSecurityDefinition`/`AddSecurityRequirement`) — Authorize button now available for testing protected endpoints directly in Swagger

## Step 4 checklist — was labeled "Step 3"
- [x] Domain: `ConversationThread`, `InboundMessage`, `TaskItem` entities (named to avoid `System.Threading.Thread`/`System.Threading.Tasks.Task` clashes); `NotifyHubTaskStatus`, `TaskPriority` enums
- [x] Domain: `OptOutKeywordMatcher` (FR-006), `TaskDueDateDefaults` (FR-008 priority→offset), `RecurrenceCalculator` (BR-007) — pure, unit-tested
- [x] EF Core configs + migration (`AddInboxAndTasks`) for `threads`/`inbound_messages`/`tasks`, plus the real FK+index on `outbound_messages.thread_id` deferred from step 3
- [x] `POST /api/webhooks/inbound` (FR-005/FR-006, shared-secret authenticated): routes by phone to a find-or-create thread (race-safe via unique index + retry-on-conflict, BR-012's schema note), STOP-keyword opt-out sets `patients.opt_out_at` + audits, pushes a SignalR event
- [x] `InboxHub` at `/hubs/inbox` (FR-007): JWT via `access_token` query param (browsers can't set ws headers), broadcasts on new inbound message and thread assignment
- [x] `GET/POST /api/threads`, `GET /api/threads/{id}` (resets unread count, §6c), `POST /api/threads/{id}/messages` (staff ad-hoc reply, BR-001b/BR-008), `POST /api/threads/{id}/assign` (409 on double-assign, BR-012), `POST /api/threads/{id}/tasks`
- [x] `GET/PATCH /api/tasks`, `GET /api/tasks/{id}`: recurrence spawn on completion (BR-007), escalated-status auto-revert on assignee action (BR-014)
- [x] `EscalationJob` + `EscalationWorker` (Worker, 60s poll): flags overdue tasks, reassigns to fallback Admin, audits "escalation"+"assignment" (BR-004)
- [x] `MessageDispatcher` fixed: BR-001a opt-out check immediately before the gateway call (blocks + audits "blocked"); ad-hoc (templateless) messages no longer NPE on render (skip render, `RenderedBody` already set at creation)
- [x] Config wired: `Escalation:PollIntervalSeconds` (Worker appsettings, non-secret so not threaded through `.env`)
- [x] **Shared inbox screen** (`/inbox`, §6b): thread list with unread badge, conversation panel merging inbound/outbound in order (BR-008), reply box (disabled + banner when patient opted out, BR-001b), assign button, inline "make task" form, empty-inbox and empty-conversation states (§6c), auto-scroll only when already at the bottom (§6c), real-time updates via `useInboxHub`
- [x] **Task board screen** (`/tasks`, §6b): status-filtered list, priority + status badges (color+text label, §6c), complete/assign-to-me row actions, "new task" form (thread picker + priority + due date — the API only creates tasks against a thread, no standalone endpoint, see deviations)
- [x] `package-lock.json` generated and committed, CI switched from `npm install` to `npm ci` with caching (resolves a gap flagged since step 1/2)
- [x] Tests: 25 new Domain unit tests (opt-out matcher/due-date defaults/recurrence) + 19 new integration tests (inbound routing, thread assign/reply/task-creation, task recurrence/escalation-revert, escalation job, dispatcher opt-out block). **82/82 passing** (50 Domain, 32 Integration)
- [x] **Self-verified live in Docker this session** (see "Needs your verification" for what's left to you)

## Step 5 checklist
- [x] Domain: `ReminderDueCalculator.IsDue` (pure, unit-tested) — a reminder is due once its offset window opens (`now >= scheduledAt - offsetHours`) and until the appointment itself occurs (`now < scheduledAt`); an appointment created already inside the window fires immediately rather than being skipped
- [x] Domain: `ReminderTriggerReference.Build`/`TryParse` — `appointment:{id}:reminder:{offsetHours}h:{scheduledAt.Ticks}`; embeds the appointment's `ScheduledAt` directly rather than a separate version counter (see deviations) so a reschedule produces a new reference automatically, and `TryParse` safely rejects unrelated formats (e.g. the pre-existing seed data's `appointment:{id}:created`)
- [x] Infrastructure: `ReminderScheduler.RunAsync` (`NotifyHub.Infrastructure/Reminders/ReminderScheduler.cs`) — reuses the exact same `outbound_messages`/dispatcher pipeline as every other message, no parallel send path. Two passes per run: `SupersedeStaleRemindersAsync` (BR-010 — any still-`Queued` reminder whose embedded `ScheduledAt` no longer matches the appointment's current value, e.g. because it was rescheduled, is marked `Superseded`) then `CreateDueRemindersAsync` (FR-009/BR-003 — queues a reminder for each due appointment×offset-template pair, checked against `outbound_messages.idempotency_key` first so re-running is a no-op)
- [x] Domain: `MessageStatus.Superseded` added — terminal, never picked up by `MessageDispatcher`'s `Status == Queued` query, so no dispatcher/API change was needed to make it safe
- [x] Worker: `ReminderWorker` (`Reminders:PollIntervalSeconds`, default 900 = 15 minutes per the locked §14 decision), registered alongside `DispatcherWorker`/`EscalationWorker` in `NotifyHub.Worker/Program.cs`
- [x] Tests: 2 new Domain test files (`ReminderDueCalculatorTests` — window-boundary cases including exactly-at-open and appointment-already-inside-window; `ReminderTriggerReferenceTests` — round-trip + rejects malformed/unrelated references), 1 new integration test file (`ReminderSchedulerTests` — due-appointment queues exactly once across two runs (BR-003), not-yet-due queues nothing, and reschedule supersedes the old queued reminder while queuing a fresh one, re-run-safe afterward too (BR-010)). **101/101 fast backend tests passing** (64 Domain, 37 Integration InMemory) + the existing MySQL-only race test (not re-run this session, unaffected by this change)
- [x] **Docker/live verification** — confirmed live. `Reminders:PollIntervalSeconds` temporarily set to 30 (from the 900s/15min default) to avoid a 15-minute wait; `PatientAppointmentSeedStep`'s patient 1 (`now+1day`) and patient 2 (`now+2days`) appointments both fall inside the 48h reminder window from stack startup, and the first `ReminderWorker` poll queued their reminders as expected — no duplicates, no unexpected supersede/skip behavior, matches `ReminderSchedulerTests`. Poll interval reverted back to the 900s default after the test.

## Step 6 checklist
- [x] Confirmed all 5 FR-011 event types (send, receipt, opt-out, assignment, escalation) were already audited by steps 3–5 — no new writes needed, just verified via a full-repo grep of `AuditLogger.Add` call sites
- [x] `GET /api/audit` (Admin-only, first endpoint in the codebase needing `[Authorize(Roles="Admin")]` instead of the default any-authenticated policy) — filters `actor`/`action`/`from`/`to`, paginated via the existing `PagedResult<T>` pattern
- [x] `GET /api/audit/mine` (default authenticated) — same filters minus `actor`, server sets `actor` to the caller's own username regardless of any client-supplied value
- [x] `PATCH /api/templates/{id}` added — §6b requires "create/**edit**" for the Templates screen but the endpoint only had `GET`/`POST`; added to close that gap, same PATCH-only-non-null-fields convention as `TasksController.Update`
- [x] `PerformanceSeedStep` (FR-010): ~1,000 new synthetic patients/threads (thread count scales with the target message count, `~50 messages/thread`, clamped 10–1,000), 90/10 outbound/inbound split, all outbound messages terminal-status (Delivered/Failed mix, none `Queued`) so `DispatcherWorker`'s live poll never picks any of them up, batched inserts (`AddRange`/`SaveChangesAsync` in chunks of 2,000, `AutoDetectChangesEnabled=false` during the loop), own idempotency check (patient-name marker prefix) independent of `DemoOutboundMessageSeedStep`'s
- [x] `targetMessageCount` is a constructor parameter, read from `Seed:PerformanceMessageCount` in `Program.cs`'s DI registration (default 50,000) — **all three test factories override this to a small number** (`CustomWebApplicationFactory`: 50, `MySqlWebApplicationFactory`: 100) so booting the Api pipeline in every integration test doesn't also seed 50k rows per fixture; caught this during test-writing when a first draft of the idempotency test shared the auto-seeded DB and got a false pass
- [x] **Templates & reminder rules screen** (`/templates`, §6b): list + create form + inline per-row edit form, same `NewTaskForm`/`TaskBoardPage` conventions (native `<select>` for enums, toast wording without "successfully")
- [x] **Audit log screen** (`/audit`, §6b): role-branches on `user.role` — Admin gets an actor filter + `/api/audit`, Staff gets `/api/audit/mine` with no actor filter (server-enforced anyway); action dropdown, from/to date filters, paginated table, empty state
- [x] Tests: `AuditControllerTests` (Admin sees all actors, Staff forbidden from `/api/audit`, `/mine` scopes to caller, date-range filter), `TemplatesControllerTests` (PATCH applies only provided fields, 404 on unknown id, rejects invalid trigger type), `PerformanceSeedStepTests` (re-run doesn't duplicate, no `Queued` status ever created, no-op when no templates exist — uses its own isolated InMemory `DbContext` rather than the shared factory, precisely to avoid the auto-seed collision above)
- [x] **`ThreadsController.Detail` message-history pagination** (fixed per your review feedback on the first step-6 pass, before treating step 6 as done): previously loaded a thread's *entire* inbound+outbound history unpaginated via `.Include()` — replaced with `GetMessagesPageAsync`, a merge-pagination of the two independently-ordered tables that never selects more than `skip+pageSize` rows from either one (proof of correctness is in the method's doc comment and mirrored in `CODEBASE_MAP.md` §5). `ThreadDetailDto.Messages` is now `PagedResult<ThreadMessageDto>` (default page size 25, max 100, same `PagedResult<T>.Clamp` as every other paginated endpoint — §11a). Page 1 = most recent messages (chat-style); `ConversationPanel.tsx` gained a "Load earlier messages" button that fetches and prepends older pages on demand, so history beyond page 1 is still reachable from the UI, just not loaded by default. New test: `ThreadsControllerTests.Detail_PaginatesMessages_DoesNotReturnFullHistory` — seeds 60 messages, asserts page 1 returns exactly 25 (not 60), and that pages 1+2 combined reconstruct the correct most-recent-50 in chronological order with zero overlap, proving the two-table merge is correct, not just "returns some subset". This closes the Final review checklist item logged during the first step-6 pass.
- [x] **Docker/live verification** — confirmed against the real 50,000-row seed on live MySQL. `PerformanceSeedStep` took ~21.4s (started `06:06:18.172`, app began listening at `06:06:39.608`, derived from `docker compose logs --timestamps api`). Timed checks (admin JWT, `curl -w "%{time_total}"`): `GET /api/threads?pageSize=25` 0.098s, `GET /api/audit?pageSize=25` 0.029s, `GET /api/threads/{id}?pageSize=25` 0.053s (thread id 1000, from the first call's results). All well within an acceptable range at this scale.

## Step 7 checklist
- [x] `README.md` — project overview, one-command run, screens, test/coverage commands, CI summary, documentation index, FR-012 git-history note
- [x] `docs/adr/0001-outbound-queue.md`, `0002-dispatcher-hosting.md`, `0003-rbac-model.md` (FR-016) — each states the decision, the rejected alternatives, and why they were rejected, grounded in what's actually implemented (file:line citations), not restated spec prose
- [x] `docs/SECURITY.md` (FR-018) — sub-criteria (a)-(e) plus a full OWASP Top-10 (2021) walkthrough against the real codebase
- [x] `docs/AI_USAGE_LOG.md` (FR-019) — phases table, 3 representative sessions (1 frontend: shared inbox + SignalR; 1 Domain: retry/backoff + a real spec-ambiguity catch; 1 backend+docs: audit/seed/this step), the `ThreadsController.Detail` unpaginated-history scoping mistake as the required "AI was wrong" example with its fix, and 2 examples of AI used beyond code gen (npm dependency root-causing, this documentation step's own coverage-measurement work). Flagged explicitly (not silently assumed) that Claude Code has no visibility into any separate ChatGPT session used during early requirements drafting — left as an open note for Zohaib to fill in if applicable, rather than fabricated.
- [x] **FR-013 coverage number, measured not estimated**: `dotnet test NotifyHub.sln --filter "Category!=MySql" --collect:"XPlat Code Coverage"` + `dotnet-reportgenerator-globaltool`, filtered to the `NotifyHub.Domain` assembly → **94.2% line / 97.7% branch / 92.1% method coverage** (147/156 coverable lines), comfortably over the 70% bar. Methodology and per-class breakdown in `docs/coverage/DOMAIN_COVERAGE.md` — deliberately measured across both `Domain.Tests` (pure logic, ~100% on every business-rule class) and `Integration.Tests` (exercises the plain-POCO entity classes via EF Core) together, since running Domain.Tests alone understates it (56.3%, entity property setters never touched by pure unit tests) and would have been a misleading number to publish.
- [x] Confirmed already-satisfied requirements rather than re-building them: FR-012 (incremental commit history — already true, `git log`), FR-014 (CI green on every push — `.github/workflows/ci.yml` already builds+tests both .NET and web on every push), FR-015 (`docker-compose up` already one-command, migrations+seed automatic), FR-017 (Swagger already reachable at `/swagger`, confirmed via `Program.cs:116-117`)
- [x] **Step 7 fix round, per Zohaib's review**: (1) checked whether Swagger needed gating behind `IsDevelopment()` — it already was (`Program.cs:114-118`), so `docs/SECURITY.md`'s A05 note was corrected instead of touching code (it's reachable in this build only because `docker-compose.yml` sets `ASPNETCORE_ENVIRONMENT=Development`). (2) Added a real dependency-vulnerability scan to CI (`dotnet list package --vulnerable --include-transitive` + `npm audit --audit-level=high`, both gating the build) — running it surfaced 4 genuine pre-existing High-severity transitive advisories (`Microsoft.Extensions.Caching.Memory 8.0.0`, `System.Text.Json 8.0.0` ×2, `System.Net.Http 4.3.0`, `System.Text.RegularExpressions 4.3.0`), not hypothetical ones; fixed by pinning direct `PackageReference`s to patched versions in the 5 affected `.csproj` files (Api/Worker/Infrastructure/Domain.Tests/Integration.Tests) — re-ran the scan clean, rebuilt, and all 112 fast tests still pass. `docs/SECURITY.md`'s A06 row and summary updated to reflect the closed gap rather than left stale.
- [x] **`UnreadCount` atomicity, the last open Final review checklist item**: investigated and fixed — see the Final review checklist below for the read-then-write race found, the `ExecuteUpdateAsync` fix, and the new concurrent test proving it. Nothing remains open across the whole build.

## Step 8 checklist

All 14 increments below are independently committed; each backend increment shipped with its own
EF Core migration and integration tests, each frontend increment was typechecked/built and
verified end-to-end against the live Docker stack (screenshots taken, not just compiled).

1. **Schema foundation** — `TaskItem.Description`/`TaskType`/`IsActive`, `User.Status`/`FullName`,
   new `TaskType`/`UserStatus` enums. `IsActive` is a list-filter flag only (Zohaib confirmed:
   "just a checkbox... filter defaults to Active"), deliberately independent of the pre-existing
   workflow `Status` enum — no escalation/recurrence/BR-014 logic touched. Migration
   `AddTaskAndUserFields` — EF's generated `AddColumn` defaults were hand-corrected post-generation
   (`IsActive` → `true`, `Status`/`TaskType` → `"Active"`/`"General"` instead of the generated
   `false`/`""`, since `""` isn't a valid enum member and would fail to deserialize existing rows).
2. **Users backend + fallback resolver** — `UsersController` (list/create/assignable, Admin-only
   except `assignable`). `PATCH /api/users/{id}/status`: transitioning to Inactive/OnLeave
   auto-forwards that user's non-terminal tasks to a fallback Active Admin, atomically with the
   status write, audited (`action:"forward"`). `FallbackUserResolver` extracted from
   `EscalationJob`'s previously-inline "lowest-id Admin" lookup, now Active-only and reused by both
   paths.
3. **Global read-only enforcement** — `ActiveUserRequiredFilter`, registered in the same
   `MvcOptions.Filters` list as the pre-existing `AuthorizeFilter` (so every current/future
   controller is covered automatically, not opt-in per-attribute). Checks live DB `User.Status`
   (not JWT claims — a claims check would let a just-deactivated user keep mutating for up to
   `Jwt:AccessTokenMinutes`). Skips safe HTTP verbs and `[AllowAnonymous]` actions so Inactive/
   OnLeave users can still log in/out (§7: "read-only," not "locked out").
4. **Manual task forward** — `POST /api/tasks/{id}/forward` (`{targetUserId, note?}`), always
   audited (closing a pre-existing gap where `PATCH /api/tasks/{id}`'s `AssignedStaffId` branch
   was never audited), broadcasts a new `taskAssignmentChanged` SignalR event. Deliberately leaves
   workflow `Status` untouched — forwarding an Escalated task keeps it Escalated for the new
   assignee; BR-014's auto-revert still only fires when the *current* assignee acts on their own
   task.
5. **Task filters + Description auto-populate** — `TasksController.List` gains `description`/
   `patientName`/`dueFrom`/`dueTo`/`isActive` (defaults `true` when omitted). `ThreadsController.
   CreateTask`'s `Description` auto-populates from the thread's most recent message (inbound or
   outbound, whichever is newer) when the client omits it — compares only each table's single
   most-recent row, same "no full-history load" discipline as `GetMessagesPageAsync` (§5/FR-010).
6. **Frontend user roster** — `useAssignableUsers()` (`GET /api/users/assignable`) replaces
   `TaskBoardPageV2`'s old "dedupe usernames off already-fetched tasks" hack, which could never
   surface a user with zero tasks currently assigned to them.
7. **Frontend Task module UI** — Description/TaskType fields on create, Active/Inactive toggle +
   Forward dialog on `TaskDetailSheet`, full filter bar on `TaskBoardPageV2` (Description/Patient/
   Due date/Status/Active/Assignee). Due-date default (today−6/today 23:59) reuses a newly
   extracted `src/lib/dateRangeFilter.ts` util — previously this exact logic was duplicated
   verbatim between `AuditLogPage.tsx` and `AuditLogPageV2.tsx`; both now consume the same util
   too.
8. **Bookmarks + Template.IsActive (backend)** — new `Bookmark` entity/CRUD (Admin-only writes),
   seeded with the two merge fields `TemplateRenderer` actually resolves at send time
   (`{{patient_name}}`, `{{appointment_time}}`) rather than fabricating unresolvable ones.
   `MessageTemplate.IsActive` (defaults `true`) + `GET /api/templates?isActive=` filter.
9. **Bookmark dropdown + template filter (frontend)** — `TemplateForm` gains an "Insert bookmark"
   dropdown (inserts at the textarea's cursor position via a ref) and an Active checkbox;
   `TemplatesPageV2` gains an Active/Inactive/All filter + inactive badges.
10. **Messaging backend** — `OutboundMessage.ScheduledAt` (dispatcher's due-query now also checks
    it). New `SystemSetting` key-value table + typed `SettingsService`, backing Quiet Hours
    (`QuietHoursCalculator`, handles the wrap-past-midnight case, e.g. 21:00–08:00) and per-patient
    rate limiting (`RateLimitChecker`) — both pure Domain calculators matching
    `RetryBackoffPolicy`'s shape, **both default disabled** so existing dispatch behavior is
    unchanged until an Admin opts in via the new `SettingsController`
    (`GET`/`PATCH /api/settings`, `GET /api/settings/system-info`). `MessageDispatcher` gates its
    whole batch on Quiet Hours before touching any message. New `POST /api/threads` for
    staff-initiated conversations with a brand-new patient (creates Patient+Thread+first message
    in one call, 409 on duplicate phone).
11. **New-conversation flow + Settings tabs (frontend)** — `ThreadList` gains a "New conversation"
    dialog. `SettingsPage` rebuilt from a single legacy/redesign toggle into 7 tabs (General/SMS/
    Task/Template/Notification/User Management/System) — SMS and User Management are fully live;
    Template hosts Bookmark CRUD; Task/System are read-only; General/Notification are thin
    client-only placeholders (no concrete field list was ever specified for these two, so nothing
    was invented). **The legacy/redesign toggle UI is dropped here** — `UIVersionContext`'s default
    (`"redesign"`) and the legacy page files themselves are untouched, only the manual switch
    control is gone (no component calls `setVersion`/`toggleVersion` anymore after this).
12. **Dashboard backend** — `GET /api/dashboard/summary`: caller's own task counts by status (+
    org-wide for Admins), overdue counts, unread-thread count, a 10-row recent-activity feed
    (Admin sees everyone's actions, Staff sees their own — mirrors `AuditController`'s existing
    split). Pure read-side aggregation, no new business logic, built last since it depends on
    Task/Thread/Audit all being stable.
13. **Dashboard frontend + top-nav task widget** — `/` no longer redirects to `/inbox`; it renders
    the new `DashboardPage` (stat cards, org-wide card for Admins, recent activity, quick links).
    `TaskNavWidget` in `AppShell`'s header: a `Popover` badge-counting the caller's non-terminal
    assigned tasks; selecting one navigates to `/tasks?task={id}`, reusing the pre-existing
    deep-link mechanism that already opens `TaskDetailSheet` rather than building a second modal
    renderer.
14. **Polish** — Inbox composer gained an "Insert template" dropdown and a "Schedule" toggle
    (`datetime-local` input, passed as `scheduledAt`) on `ConversationPanelV2`. `AppShell`'s
    icon-only Settings button replaced with a text "Settings" `NAV_LINKS` entry (gear icon
    removed). Hand-authored bell-glyph favicon (`public/favicon.svg`, indigo rounded-square +
    white bell outline, matching `LoginPageV2`'s existing lucide `Bell` mark styling) linked from
    `index.html` — first favicon this repo has ever had. Seed data: `PatientAppointmentSeedStep`'s
    10 patients renamed from "Patient 01".."10" to realistic names (John Donald, Leonard Allen,
    Wasim Khan, Mateen Anjum, Emily Carter, Robert Chen, Fatima Ali, Michael Brown, Olivia Turner,
    Ahmed Hassan); `UserSeedStep`/`SecondStaffSeedStep` set `FullName` (Admin → "Dr. Jawad", Staff →
    "Sarah Wilson", Staff2 → "David Lee") while login `Username`s stay config/env-driven as before
    (security-sensitive, not renamed). New `SystemSettingSeedStep` seeds default rows for every
    known setting key, idempotent per-key (not "any setting exists") so a future new key isn't
    silently skipped on an already-seeded install.

**Tests**: 78 Domain (14 new: `RateLimitCheckerTests`, `QuietHoursCalculatorTests`) + 82 Integration
(30 new across `UsersControllerTests`, `ActiveUserRequiredFilterTests`, `BookmarksControllerTests`,
`SettingsControllerTests`, `DashboardControllerTests`, plus additions to `TasksControllerTests`,
`ThreadsControllerTests`, `MessageDispatcherOptOutTests`, `TemplatesControllerTests`) — **160/160
fast backend tests passing**. Frontend: `tsc --noEmit` and `npm run build` clean after every
increment. No new frontend unit tests (matches the pre-existing "no Vitest/RTL suite" convention,
§ Known limitations) — every UI increment was instead driven live against the real Docker stack
with Playwright (ad hoc verification scripts, not a committed suite) and screenshotted.

**Docker/live verification** — done incrementally, not just at the end: rebuilt `api`/`web` images
and recreated containers after each backend-touching increment, drove the actual app in a
headless browser (login → target screen → interact → screenshot) rather than trusting
build-success alone. Caught and fixed two real bugs this way, not in code review:
- The migration-generated `AddColumn` defaults (`IsActive`→`false`, `Status`/`TaskType`→`""`)
  would have corrupted every pre-existing row's `IsActive`/left invalid enum strings that fail to
  deserialize on read — caught before the migration was ever applied to a real database, by
  reading the generated migration file rather than trusting `dotnet ef migrations add`'s output
  blindly (see increment 1).
- The favicon returned `text/html` (Vite's SPA fallback) instead of `image/svg+xml` after the
  first attempt, because `public/favicon.svg` was created *after* the last `docker compose build
  web`, so the running image simply didn't contain it yet — caught by checking the actual response
  `Content-Type` with `curl`, not just confirming the browser tab didn't error. Fixed by rebuilding.
- Also caught (increment 14): after resetting the dev DB volume to verify the new seed names, the
  first `GET /api/users` still returned `fullName: null` for all three seeded accounts — the `api`
  image itself hadn't been rebuilt since the seed-step code changes (only `web` had, for the
  favicon fix), so it was still running increment-13's binary. Rebuilding `api`/`worker`/`web`
  together and resetting the volume again resolved it; confirmed via direct MySQL query
  (`SELECT Id, Name, Phone FROM patients`) rather than trusting a single UI screenshot.
- **Destructive-action flag**: resetting the dev DB (`docker compose down -v`) was flagged to
  Zohaib and confirmed before running, per his standing preference from an earlier session.

## What's implemented
- **Domain**: `ConversationThread`/`InboundMessage`/`TaskItem` per §7. `OptOutKeywordMatcher.IsOptOutRequest` — whole-body exact match (trimmed, case-insensitive) against STOP/UNSUBSCRIBE/CANCEL/END/QUIT, not substring, so "please stop calling" isn't misclassified. `TaskDueDateDefaults.DefaultDueAt` — urgent=4h/high=1d/medium=3d/low=7d from creation. `RecurrenceCalculator.NextOccurrence` — due-date-anchored (no drift), stops when the next due date reaches `recurrenceEndDate` or `occurrenceCount` exceeds `recurrenceMaxOccurrences`.
- **Infrastructure**: EF configs for the three new tables, real FK from `outbound_messages.thread_id` → `threads.id` (was a plain nullable column since step 3), `(thread_id, created_at)` index. `EscalationJob.EscalateOverdueTasksAsync` — batches overdue, non-escalated/completed/cancelled tasks, escalates + reassigns to the lowest-id Admin (inference — BR-004 doesn't specify which Admin when more than one exists), leaves `original_owner_id` untouched (BR-007d). `MessageDispatcher.DispatchOneAsync` now checks `patient.OptOutAt` before every gateway call (not just at message creation) and skips template rendering for ad-hoc (templateless) messages.
- **Api**: `WebhooksController.Inbound` — find-or-create thread with a genuine race-safe pattern (optimistic insert, catch `DbUpdateException` on the unique index, re-read the winner). `ThreadsController`/`TasksController` — no `[Authorize(Roles=...)]` needed since Admin-or-Staff matches the default authenticated policy (same reasoning as `TemplatesController`). `AssignRequest.StaffId` null = self-assign; non-null targeting someone else requires Admin (403 otherwise). `InboxHub` — JWT read from `access_token` query string only for `/hubs/*` paths (doesn't weaken auth elsewhere), configured via a `JwtBearerEvents.OnMessageReceived` hook.
- **Worker**: `EscalationWorker` — periodic poll (default 60s, `Escalation:PollIntervalSeconds`), same error-retry-backoff resilience posture as `DispatcherWorker`. Both new-table-dependent workers throw transient "table doesn't exist" errors for a few seconds at cold start if they poll before Api's `Database.MigrateAsync()` finishes — self-heals via the existing 5s error-retry delay; confirmed in this session's fresh-volume test, not a regression (same known gap since step 1/2, since Worker still doesn't gate on Api's migration completing).
- **React**: `AppShell` (top nav: Inbox/Task board/sign-out, mounts the one shared `useInboxHub` SignalR connection so it's live across every authenticated screen, not just Inbox). `InboxPage`/`ConversationPanel`/`CreateTaskForm` — TanStack Query hooks (`useThreads`, `useThread`, `useReplyMutation`, `useAssignMutation`, `useCreateTaskMutation`) invalidate on both direct mutation and SignalR push. `TaskBoardPage`/`NewTaskForm` — `useTasks`/`useUpdateTaskMutation`. `PriorityBadge`/`TaskStatusBadge` — color+label per §6c. Toasts follow the existing past-tense, no-"successfully" convention from `LoginPage`.
- **Reminders (step 5)**: `ReminderScheduler` (Infrastructure) polls appointments due for a 48h/2h reminder and queues them onto `outbound_messages` — same table, same `MessageDispatcher`/gateway/webhook pipeline as every other message; no parallel send mechanism. Idempotent re-runs (BR-003) via the existing `idempotency_key` unique-index pattern, checked explicitly before insert. Reschedule handling (BR-010) is poll-based rather than event-driven: since no appointment-management endpoint exists (appointments are stub data, §7/out-of-scope), there's no "on reschedule" hook to call — instead, `trigger_reference` embeds the appointment's `ScheduledAt`, so every scheduler run can detect a mismatch between a still-`Queued` reminder's embedded value and the appointment's current one and mark it `Superseded` before queuing a fresh reminder. `ReminderWorker` (Worker) polls every 15 minutes (`Reminders:PollIntervalSeconds`), same error-retry-backoff posture as `DispatcherWorker`/`EscalationWorker`.
- **Audit + performance seed (step 6)**: `AuditController` — `GET /api/audit` (Admin-only) and `GET /api/audit/mine` (Staff, own actions), both filterable by actor (admin route only)/action/date-range and paginated via the existing `PagedResult<T>` pattern. `TemplatesController` gained `PATCH /api/templates/{id}` to close a §6b gap (edit wasn't previously possible). `PerformanceSeedStep` seeds ~50,000 historical outbound/inbound messages across ~1,000 new synthetic threads (not concentrated on the small existing demo patient set) so `ThreadsController.List`'s pagination gets real exercise without ballooning any single thread's message count. Two new screens close out §6b's screen list: Templates & reminder rules (`/templates`) and Audit log (`/audit`). `ThreadsController.Detail`'s message history is now paginated too (`GetMessagesPageAsync`, merge-paginates inbound/outbound without a full-history load) — fixed after your review flagged it as an FR-010 violation, not left as a follow-up.
- **Tests**: `OptOutKeywordMatcherTests`, `TaskDueDateDefaultsTests`, `RecurrenceCalculatorTests` (Domain). `InboundWebhookTests`, `ThreadsControllerTests`, `TasksControllerTests`, `EscalationJobTests`, `MessageDispatcherOptOutTests` (Integration) — `EscalationJob`/`MessageDispatcher` aren't hosted inside `WebApplicationFactory` (Worker-only), so both are instantiated directly against the same `DbContext`/`"self"` HttpClient, same pattern step 3's `OutboundPipelineTests` established. Tests within a shared `IClassFixture` class use distinct synthetic patients/phone numbers per test method to avoid cross-test DB pollution (a real bug of this shape was caught and fixed in step 3's retry test). No frontend test suite exists yet (no Vitest/RTL setup) — not required by FR-013 (Domain coverage + one integration test), and out of scope for a 3-day build; the React screens are verified by compile/build/manual walkthrough only.

## Needs your verification
This step was self-verified live in Docker this session — significantly more than steps before it, where no Docker was available in the build environment. Still worth your own pass:
1. **Fresh-volume startup** — I ran `docker-compose down -v` (removed your running stack + MySQL volume from the step-3 review) and `docker-compose up` from scratch. All three migrations applied (`InitialCreate` → `AddOutboundPipeline` → `AddInboxAndTasks`), all 4 seed steps ran, dispatcher processed the 10 demo messages, no errors after the expected cold-start table-not-ready retries. (I flagged this teardown after the fact last time — you asked me to flag it *before* acting going forward, noted for future steps.)
2. **Full manual walkthrough I ran via curl** (still live in your Docker stack — thread 1 assigned to `staff` with a task created, thread 2 opted out via STOP): inbound webhook → thread creation → `GET /api/threads` shows it unread; assign → 200, re-assign → 409 (BR-012); `GET /api/threads/1` → unread resets to 0, inbound+outbound merged in order (BR-008); staff reply → queued → dispatched → `Delivered` within seconds; STOP keyword → `patientOptedOut: true`; reply attempt to opted-out thread → 400 (BR-001b); task creation with no body → `Medium` priority, due date ≈ now+3 days (FR-008 defaults); restart idempotency → no duplicate patients/threads/tasks. All matched expectations.
3. **React screens — now covered by the Playwright e2e suite** (see "Playwright e2e suite" section above), including a real two-browser-context SignalR check (reply in one tab, appears live in the other with no reload). 11/11 passing. Still worth your own click-through for anything the automated suite doesn't assert on (visual polish, auto-scroll-only-when-at-bottom behavior, etc.) — the suite proves correctness, not the full UX feel.
4. **Escalation timing — confirmed live.** `Escalation:PollIntervalSeconds` was temporarily set to 10 (from the 60s default) and the worker restarted; a task was created via `POST /api/threads/{id}/tasks` with a due date ~8s in the future (assigned to `staff`, thread pre-assigned to `staff` so the task wasn't already Admin-owned) — passes creation-time validation since it's still in the future at that instant, then becomes overdue almost immediately. The Worker's own poll loop (not just `EscalationJobTests`) picked it up within one 10s cycle: status flipped `Open` → `Escalated`, `assignedStaffId` reassigned `2` (staff) → `1` (fallback Admin, BR-004), `originalOwnerId` unchanged at `2` (BR-007d). Audit log shows one `assignment` event (`"auto-reassigned to Admin (was 2)"`) and an `escalation` event (`"overdue since ..."`), both `entityType=TaskItem, entityId` matching the task. Poll interval reverted to 60 afterward.
   - **Side-finding, not a bug**: polling `GET /api/tasks/{id}` *as the task's current assignee* triggers BR-014's auto-revert (`Escalated` → `InProgress`) on every call — expected per `TasksController.Detail`, "opening an escalated task is itself an action taken by the assignee." A first attempt at this verification polled the task with the Admin's own token after Admin became the assignee, which silently un-escalated it every ~2s, causing the job to re-escalate it again next cycle (repeated `escalation` audit rows 10s apart). Not a defect — just means live-checking an escalated task's status should be done as a different user (or via the audit log) rather than by repeatedly `GET`-ing it as its own assignee.

## Final review checklist
- [x] ~~Confirm UnreadCount increment in the inbound webhook path is atomic~~ — **investigated and fixed**: it was read-then-write (`thread.UnreadCount++` on a tracked entity, flushed by `SaveChangesAsync`), a genuine lost-update race under concurrent inbound webhooks for the same thread, not already-safe. Fixed in `WebhooksController.cs` (`Inbound` action) by switching to `db.Threads.Where(...).ExecuteUpdateAsync(s => s.SetProperty(t => t.UnreadCount, t => t.UnreadCount + 1))` — an atomic `UPDATE ... SET UnreadCount = UnreadCount + 1` with no read-modify-write window. Proven by a new real-MySQL test, `InboundWebhookThreadRaceMySqlTests.ConcurrentInbound_ForExistingThread_IncrementsUnreadCountExactlyN` — 30 concurrent inbound requests against one pre-existing thread, asserts `UnreadCount` lands at exactly 31 (would land short against the old code). This was the last open item on this checklist — nothing remains open.
- [x] ~~`ThreadsController.Detail` loads a thread's entire inbound+outbound message history unpaginated~~ — **fixed during step 6** after you flagged it as a real FR-010 violation rather than a deferrable nice-to-have. See the Step 6 checklist and §5/§3 of `CODEBASE_MAP.md` for the merge-pagination approach and its correctness argument.

## Documented deviations from PROJECT_CONTEXT.md
- **`tasks.status` gets a 5th value, `Cancelled`** — approved by Zohaib 2026-07-11, PROJECT_CONTEXT.md §11a updated to list all 5 values. BR-007b requires a way to end a recurring series without completing it ("cancelling ends the series"), and the original 4-value list had no state for that.
- **Ad-hoc staff replies flow through the exact same `outbound_messages`/dispatcher pipeline as system sends**, not a separate immediate-send path. This was the only reading consistent with BR-001a ("dispatcher checks patients.opt_out_at immediately before calling the gateway... applies to both system-dispatched and staff ad-hoc messages") — that sentence only makes sense if ad-hoc messages also pass through the dispatcher. `RenderedBody` is set directly at creation (no template to render at send time); the dispatcher's `RenderAsync` step is skipped when `TemplateId` is null.
- **"Blocked" audit action added** (not one of FR-011's 5 named event types) for messages the dispatcher refuses to send to an opted-out patient. Not required by the literal spec, but leaving a send attempt with zero audit trail seemed like a worse gap than one extra, clearly-labeled action type.
- **Thread assignment target validation**: `AssignRequest.StaffId` accepts any existing `users.id` (Admin or Staff), not restricted to Staff-role users only. §4 doesn't say Admins can't work a thread themselves; restricting felt like an invented rule.
- **Escalation fallback Admin selection**: when more than one Admin exists, the lowest-id Admin is used (BR-004 doesn't specify). Deterministic but arbitrary — flag if you want a different rule (e.g. round-robin, least-loaded).
- **Escalation poll interval (60s) is an inference** — §11 says only "Periodic" for the escalation job, no number given (unlike the reminder scheduler's explicit 15 minutes, FR-009). Configurable via `Escalation:PollIntervalSeconds`.
- **Task board "reassign" is "assign to me" only**, not a full staff picker — there's no user-directory endpoint (§4: no user-management UI/screen exists), so the frontend has no way to list other staff to reassign to. `PATCH /api/tasks/{id}` does support arbitrary `assignedStaffId` server-side if a caller already knows the target id.
- **Task board "new task" requires picking a thread first** — the API only creates tasks via `POST /api/threads/{id}/tasks` (matches FR-008's "message→task" framing), so there's no standalone task-creation flow independent of a thread.
- **Reminder `trigger_reference` format uses the appointment's `ScheduledAt` tick value, not an incrementing version counter** — FR-003's acceptance-criteria example shows `appointment:{id}:rescheduled:{version}`, which would need a new `Appointment.Version` column bumped on every reschedule. Embedding `ScheduledAt` directly (`appointment:{id}:reminder:{offsetHours}h:{ticks}`) achieves the same goal — a reschedule always produces a new reference — without a schema change, and is what `ReminderScheduler` compares against to detect staleness for BR-010. Flag if you want the literal `version` counter format instead.
- **BR-010's reschedule handling is poll-based, not event-driven** — there is no `PATCH /api/appointments/{id}` (or any appointment-management endpoint at all; appointments are stub data, explicitly out of scope for a dedicated screen per §1). `ReminderScheduler` instead detects a reschedule indirectly, by comparing each still-`Queued` reminder's embedded `ScheduledAt` against the appointment's current value on every poll. This is correct and tested (`ReminderSchedulerTests`), but means a reschedule takes up to one poll interval (15 min) to be superseded, not instantaneous. If an appointment-reschedule endpoint is added later, it could call the same supersede logic synchronously instead.
- **`GET /api/audit` is Admin-only (`[Authorize(Roles="Admin")]`)** — the first endpoint in the codebase that needs the stricter role policy instead of the default any-authenticated one. Matches §4/§8 literally ("view full audit log, all actors" is Admin; Staff gets `/api/audit/mine`), not an inference.
- **`PATCH /api/templates/{id}` added, beyond your literal step-6 work-item list** — §6b's own acceptance criteria for the Templates screen says "create/**edit**", but the endpoint only had `GET`/`POST` before this step. Added rather than building an edit-less screen that contradicts its own spec row.
- **50k seed spreads across ~1,000 new synthetic patients/threads rather than the existing 10 demo patients** (~50 messages/thread on average, 90/10 outbound/inbound split, all terminal-status so the live dispatcher never touches them) — concentrating 50k messages on today's 10 patients would give 5,000/thread, which wouldn't exercise `ThreadsController.List`'s pagination at all (still just 10 rows) and would trip the just-logged `Detail()` unpaginated-load risk into a real, demo-visible slowdown. Flag if a different distribution (e.g. reusing the existing 10 patients) is actually what you wanted.
- **`PerformanceSeedStep`'s target count is capped in all three test factories** (`Seed:PerformanceMessageCount` = 50 in `CustomWebApplicationFactory`, 100 in `MySqlWebApplicationFactory`, production default 50,000) — `Program.cs` runs every registered `IDbSeedStep` unconditionally at startup with no environment gating, so without this override every integration test booting the Api pipeline would also seed 50k rows per test-class fixture.

## Documented deviations from PROJECT_CONTEXT.md (step 8 additions)
- **`TaskItem.IsActive` is a separate boolean field, not a replacement for the workflow `Status`
  enum** — Zohaib's own framing ("just provide a checkbox... filter defaults to active selected")
  confirmed this reading; the alternative (collapsing `Open/InProgress/Completed/Escalated/
  Cancelled` down to `Active/Inactive/Cancelled`) would have broken escalation/BR-014/recurrence,
  all of which key off the existing 5-value enum.
- **Forwarding a task never changes its workflow `Status`**, even when forwarding an Escalated
  task — not explicitly specified either way; chosen so BR-014's existing "auto-revert only when
  the current assignee acts on their own task" semantics stay exactly as they were, rather than
  silently redefining what counts as "acting on" a task.
- **Auto-forward on user deactivation does not check `TaskItem.IsActive`** — that flag is
  documented (above, and by Zohaib directly) as a list-filter concept only; gating auto-forward on
  it would have been an invented workflow-eligibility rule not asked for.
- **Quiet Hours and per-patient rate limiting both default to disabled** — the functional
  requirement was to *implement* both features, not to silently start blocking/throttling every
  existing message flow (including the 45,000-message performance seed and demo scenarios) the
  moment this shipped. An Admin opts in via Settings > SMS with sensible pre-filled defaults
  (21:00–08:00 UTC / 20 messages per 24h).
- **General and Notification Settings tabs are thin/client-only placeholders** — no concrete field
  list was ever specified for either, and inventing `SystemSetting` schema for unrequested fields
  felt like a worse outcome than an honest "nothing to configure here yet" tab.
- **The legacy/redesign toggle UI was removed during the Settings rebuild (increment 11), not
  deferred to a later "UI lock" step** — General is meant to be thin/read-only per the point above,
  and the toggle didn't fit any of the 7 requested tabs; `UIVersionContext`'s default and the
  legacy page files themselves are still untouched, only the manual switch control is gone.
- **Login usernames were not renamed to match the new realistic seed names** — `Seed:AdminUsername`/
  `Seed:StaffUsername`/`Seed:Staff2Username` stay config/env-driven exactly as before (security-
  sensitive, not something to hardcode); only the new `User.FullName` field ("Dr. Jawad"/"Sarah
  Wilson"/"David Lee") carries the realistic names into the UI.

## Known limitations (by design, not bugs)
- SignalR broadcasts go to `Clients.All` (every connected authenticated session) — matches "shared inbox" semantics (no per-staff filtering), consistent with §4's flat Admin/Staff visibility model, but means there's no way to scope a notification to only the assigned staff member if that's later desired.
- No stale-"Sending"-message recovery sweep (carried over from step 3, still applies).
- `{{appointment_time}}` resolution via `trigger_reference` parsing (carried over from step 3, still applies).
- Worker's dispatcher/escalation loops don't gate on Api's migration completing (no health-check endpoint to gate on) — self-heals via error-retry backoff, confirmed in this session's fresh-volume test, but produces a few seconds of expected "table doesn't exist" warnings in worker logs at cold start.
- No frontend unit test suite (Vitest/RTL) — not required by FR-013, out of scope for a 3-day build. End-to-end coverage exists instead via the new Playwright suite (11/11 passing, see above).

## How to run
```
docker-compose up
```
Requires a `.env` file at the repo root — copy `.env.example` and fill in values. `CORS__WEBORIGIN` is now comma-separated (defaults to `http://localhost:5173,http://host.docker.internal:5173` — the second entry only matters if you run the Playwright suite). `VITE_API_URL` is no longer required/passed through — the frontend derives its API target from the page's own hostname at runtime.

Seeded accounts (values from your local `.env`, not committed):
- Admin: `SEED__ADMINUSERNAME` / `SEED__ADMINPASSWORD`
- Staff: `SEED__STAFFUSERNAME` / `SEED__STAFFPASSWORD`
- Staff2: `SEED__STAFF2USERNAME` / `SEED__STAFF2PASSWORD` (optional, second Staff account for multi-staff testing)

To run the e2e suite: `docker-compose up -d` first, then see "Playwright e2e suite" above.

## Open questions
- None currently blocking.

## Change log
| Date | Step | Summary |
|---|---|---|
| 2026-07-11 | 1–2 (combined, labeled "1" at the time) | Solution skeleton, EF Core + MySQL, JWT auth (login/refresh/RBAC), seed scaffolding, Docker/compose, CI, React login screen. 20/20 tests passing. Reviewed and verified working by Zohaib. |
| 2026-07-11 | 1–2 (fix) | Swagger JWT bearer security definition added per review feedback (Authorize button). |
| 2026-07-11 | 3 (labeled "2" at the time) | Outbound messaging pipeline: templates, mock gateway, webhook receipt, retry/backoff (BR-011), audit logging (send/receipt), real Worker dispatcher, seed data for patients/appointments/templates/demo messages. 40/40 tests passing (27 Domain, 13 Integration). Docker-compose smoke test pending user review (no Docker/MySQL in build environment). |
| 2026-07-11 | 3 (fix) | BR-011 clarified per PROJECT_CONTEXT.md update: 6 total attempts (1 initial + 5 retries), all 5 backoff values used. `RetryBackoffPolicy.MaxAttempts` 5→6; tests updated. Also fixed a test-isolation bug in `OutboundPipelineRetryTests` where a prior test's requeued message could be re-claimed by a later test. 41/41 tests passing (28 Domain, 13 Integration). |
| 2026-07-11 | 3 (review) | Reviewed and verified working by Zohaib in Docker — real dispatcher confirmed end-to-end. First review round caught a stale Docker image, not a code bug; rebuilt and re-verified. |
| 2026-07-11 | 4 (labeled "3" at the time) | Inbound webhook + STOP/opt-out (FR-005/FR-006), SignalR shared inbox (FR-007), task engine with recurrence + escalation (FR-008/BR-004/BR-007/BR-014), thread reply/assign/create-task endpoints, MessageDispatcher fixes (BR-001a opt-out check, ad-hoc render fix). 82/82 tests passing (50 Domain, 32 Integration). Self-verified live in Docker this session (fresh-volume startup, full manual walkthrough via curl, restart idempotency). |
| 2026-07-11 | 4 (fix) | Step numbering corrected to match the actual 7-step plan (skeleton/auth were combined). `tasks.status` `Cancelled` addition approved and synced to PROJECT_CONTEXT.md §11a; `updates_in_Projectcontext/` deleted (fully merged). Inbox and Task board React screens built (§6b): thread list/conversation panel/reply/assign/make-task, task board with priority/status badges and complete/assign-to-me actions, real-time via SignalR. `package-lock.json` generated and committed; CI switched to `npm ci` with caching. TypeScript compiles clean, production build succeeds, dev server verified serving — not interactively browser-tested (no browser available to me); needs your pass, especially the two-tab SignalR check. |
| 2026-07-11 | 4 (bug-fix + e2e) | Fixed the 3 bugs found in your live testing: empty-200-body toast bug (Reply/Assign now 204), page-refresh logout (refresh token moved to an httpOnly cookie with silent restore on mount, §6a updated, `POST /api/auth/logout` added), and BR-014 revert-on-open (Task board rows now clickable, new `useTask(id)` hook). Built a Playwright e2e suite (`notifyhub-web/e2e/`, 11 tests/7 specs) covering login/session-persistence/reply/two-tab-SignalR/assignment-409/opt-out/unread/task-defaults/escalation-revert — found and fixed a 4th, previously unknown bug in the process (staff replies never broadcast over SignalR at all). Required test-infra fixes to make the suite runnable: Vite `allowedHosts`, runtime-derived frontend API base URL (replacing a dev-server-baked `VITE_API_URL`), multi-origin CORS. 84/84 backend tests passing (50 Domain, 34 Integration — 2 new auth-cookie tests). 11/11 e2e tests passing. |
| 2026-07-12 | (between sessions) | Real-MySQL race test added for `FindOrCreateThreadAsync` + a deadlock it found fixed (`EnableRetryOnFailure`), `CODEBASE_MAP.md` created as a standing reference, `CLAUDE.md` added to make consulting it a durable practice. |
| 2026-07-12 | 5 | Reminder scheduling (FR-009): `ReminderScheduler` (Infrastructure) + `ReminderWorker` (Worker, 15 min poll), reusing the existing `outbound_messages`/dispatcher pipeline rather than a parallel send path. `ReminderDueCalculator`/`ReminderTriggerReference` (Domain, pure/unit-tested). Idempotent re-runs via the existing idempotency-key pattern (BR-003); reschedule-supersede (BR-010) implemented poll-based since no appointment-management endpoint exists to hook a reschedule event off of — see deviations for both the trigger_reference format choice and the poll-vs-event tradeoff. New terminal `MessageStatus.Superseded`. 101/101 fast backend tests passing (64 Domain, 37 Integration InMemory), plus the existing MySQL-only race test. Not yet self-verified live in Docker — see step 5 checklist. |
| 2026-07-12 | 6 | Audit log + 50k seed (FR-010/FR-011): confirmed all 5 required audit event types were already wired by earlier steps (no new writes needed). `AuditController` (`GET /api/audit` Admin-only, `GET /api/audit/mine` Staff) — first Admin-only endpoint in the codebase. `PATCH /api/templates/{id}` added to close a §6b create/edit gap. `PerformanceSeedStep` (FR-010): ~50,000 historical messages spread thin across ~1,000 new synthetic threads, all terminal-status, own idempotency check; target count is config-driven and capped in all test factories to keep the InMemory suite fast. Two new frontend screens close out §6b: Templates & reminder rules (`/templates`), Audit log (`/audit`, role-branches Admin/Staff). Found (not fixed yet) a latent perf risk: `ThreadsController.Detail` loads a thread's full message history unpaginated — logged in Final review checklist. 111/111 fast backend tests passing (64 Domain, 47 Integration — 10 new). Not yet self-verified live in Docker — see step 6 checklist. |
| 2026-07-12 | 6 (fix) | Per your review feedback: fixed `ThreadsController.Detail`'s unpaginated message history rather than deferring it — this was a real FR-010 violation, not a nice-to-have. `GetMessagesPageAsync` merge-paginates the `inbound_messages`/`outbound_messages` tables (independently ordered by `ReceivedAt`/`CreatedAt`) without ever loading a thread's full history: pulling only `skip+pageSize` rows DESC from each table is provably sufficient to answer any page correctly. `ThreadDetailDto.Messages` is now `PagedResult<ThreadMessageDto>` (default 25/max 100, same `Clamp` pattern as every other paginated endpoint). Page 1 = most recent messages; `ConversationPanel.tsx` gained a "Load earlier messages" button to reach older history. New test `ThreadsControllerTests.Detail_PaginatesMessages_DoesNotReturnFullHistory` proves both that page 1 doesn't return the full 60-message seeded history and that pages 1+2 combined exactly reconstruct the correct chronological set with no gaps/overlap. Resolves the Final review checklist item logged in the first step-6 pass. 112/112 fast backend tests passing (64 Domain, 48 Integration). Frontend `tsc`/`vite build` clean. Not yet self-verified live in Docker. |
| 2026-07-12 | 6 (review) | Reviewed and confirmed working by Zohaib — audit log correct for both Admin (all entries) and Staff (own actions only), pagination and filters work, 50k seed correctly bypasses the live dispatcher (no `Queued` rows created). Committed (`1c6c47b`). |
| 2026-07-12 | (between steps) | Fixed a pre-existing `npm ci` ERESOLVE conflict blocking manual builds: `vite@^8.1.4` was pinned alongside an incompatible `@vitejs/plugin-react@^4.3.2` (peer range only covers vite 4-7). Not caused by any session change — `node_modules` reuse had masked it in prior `npm run build` calls. Fixed by bumping the plugin to `^6.0.3` (peer-compatible with vite 8), regenerated `package-lock.json`, verified via a from-scratch `npm ci`/`npm run build`/`npm run dev` cycle. |
| 2026-07-12 | 7 | Docs (FR-012/013/014/015/016/017/018/019), closing out the plan. `README.md` (new). `docs/adr/0001-outbound-queue.md`/`0002-dispatcher-hosting.md`/`0003-rbac-model.md` (FR-016). `docs/SECURITY.md` — OWASP Top-10 self-assessment + FR-018(a)-(e) (FR-018). `docs/AI_USAGE_LOG.md` — phases, 3 sessions incl. 1 frontend, the `ThreadsController.Detail` pagination scoping mistake as the required "AI was wrong" example, 2 examples of AI beyond code gen (FR-019). `docs/coverage/DOMAIN_COVERAGE.md` — real measured FR-013 number: 94.2% line coverage on `NotifyHub.Domain` via `dotnet test --collect:"XPlat Code Coverage"` + `reportgenerator`, methodology and per-class breakdown documented (FR-013). FR-012/014/015/017 confirmed already satisfied by existing work, not rebuilt. Last numbered step — the only remaining open item across the whole build is the `UnreadCount` atomicity question in the Final review checklist (unconfirmed race, not proven to occur in practice). |
| 2026-07-12 | 7 (fix) | Per your review feedback on the two `docs/SECURITY.md` residual gaps: Swagger was already gated behind `IsDevelopment()` (`Program.cs:114-118`) — no code change needed, corrected the doc's A05 note instead of adding a redundant guard. Added a real, gating CI dependency-vulnerability scan (`dotnet list package --vulnerable --include-transitive`, `npm audit --audit-level=high`) — this immediately surfaced 4 genuine pre-existing High-severity transitive advisories (Caching.Memory 8.0.0, System.Text.Json 8.0.0 ×2, System.Net.Http 4.3.0, System.Text.RegularExpressions 4.3.0), which would have turned CI red on the very next push; fixed by pinning direct `PackageReference`s to patched versions across the 5 affected `.csproj` files, re-verified the scan is clean, solution builds, and all 112 fast tests still pass. `docs/SECURITY.md` A06 row + summary updated to match. |
| 2026-07-12 | 7 (final review) | Closed the last open Final review checklist item: `WebhooksController.Inbound`'s `thread.UnreadCount++` was confirmed read-then-write (a genuine lost-update race under concurrent inbound webhooks for the same thread), not already-safe. Fixed with EF Core's atomic `ExecuteUpdateAsync`/`SetProperty(t => t.UnreadCount, t => t.UnreadCount + 1)` — one `UPDATE ... SET UnreadCount = UnreadCount + 1` per request, no read-modify-write window — for real database providers; the fast test suite's InMemory provider can't translate `ExecuteUpdate`'s `SetProperty` (confirmed by running it — `InvalidOperationException`, not a hypothetical gap), so `Inbound` branches on `db.Database.ProviderName` (not the `IsInMemory()` extension, to avoid a test-only package reference in production code) and keeps the old tracked increment there, which is fine since InMemory tests are single-request and never exercise the race anyway. Proven atomic by a new real-MySQL test, `InboundWebhookThreadRaceMySqlTests.ConcurrentInbound_ForExistingThread_IncrementsUnreadCountExactlyN` (30 concurrent requests against one pre-existing thread, asserts `UnreadCount` lands at exactly 31 — MySQL-only, since it needs real concurrent-connection interleaving). Fast suite (`Category!=MySql`) still 112/112 green (64 Domain, 48 Integration) — this test is additive under `Category=MySql`, doesn't touch the fast-suite count. Nothing remains open across the whole build. |
| 2026-07-13/14 | 8 (14 increments) | Large post-step-7 feature set from a fresh, informal requirements list (not FR-numbered): Task module (type/description/forwarding/active-flag/filters), a Dashboard landing page, a bell favicon, UI redesign lock-in, Template bookmarks, messaging (scheduled sends/Quiet Hours/per-patient rate limiting/new-patient SMS), User Management (Active/Inactive/OnLeave with auto-forward), a 7-tab Settings module, realistic seed data, and a top-nav task widget. See "Step 8 checklist" above for the full per-increment breakdown. 160/160 fast backend tests passing (78 Domain, 82 Integration — 44 new). Frontend `tsc`/`vite build` clean throughout; every UI increment driven live against the real Docker stack (login → screen → interact → screenshot), not just compiled — caught and fixed 3 real bugs this way (a data-corrupting migration default, a stale-image favicon 404-as-text/html, a stale-image seed-name no-op) rather than in code review. Dev DB volume reset flagged and confirmed with Zohaib before running (`docker compose down -v`, local dev data only). All 14 increments committed individually. |
| 2026-07-14 | 9 (P9-00) | Locked spec `STEP9_PLAN.md` — responsive design pass across the v2 screens (`AppShell` hamburger/drawer nav, single-pane-with-back-button `InboxPageV2`/`TemplatesPageV2`, stacked-card `AuditLogPageV2` on mobile, wrapping `TaskDetailSheet` footer buttons, full-stack `DashboardPage` stat cards, `SettingsPage` tab-list wrap fix). See `CODEBASE_MAP.md` §6e for the per-file breakdown and "Step 9 checklist" above for the caveat: no browser/screenshot tool was available this session, so verification was `tsc -b`/`vite build`/container-rebuild-and-serves-200, not a real-viewport click-through like step 8's screens got — flagged, not silently claimed. Also found and documented (not fixed, out of `STEP9_PLAN.md` scope) pre-existing e2e-suite staleness: `loginViaUi` still waits for `**/inbox`, which stopped being the post-login redirect back in increment 13. |
| 2026-07-14 | 9 (P9-01) | 5 quick fixes, frontend-only (no schema changes): removed the command palette entirely (`command-palette.tsx`/`quick-create-template-form.tsx` deleted, `AppShell.tsx` unwired); Task Forward dialog excludes the current assignee; `TaskDetailSheet` auto-closes on any action taken from it (success-gated for toggle-active/forward, unconditional for assign-to-me/complete since those props are void-typed); `NewTaskForm`/`CreateTaskForm` split their due-date input into a required date + optional time (defaults 00:00); `OffsetHours` removed from the Templates UI (`template-form.tsx`/`TemplatesPageV2.tsx`), backend column/field untouched, create sends a fixed placeholder to satisfy the still-required backend validation. See `CODEBASE_MAP.md` §6f. Flagged a plan-file typo (Reminder Offset settings mislabeled "P9-10", actually P9-08). `tsc -b`/`vite build` clean, `docker compose up -d --build web` verified serving. |
| 2026-07-14 | 9 (P9-02) | Root cause fixed: `WebhooksController.GatewayReceipt` now broadcasts a new `messageStatusUpdated {threadId, messageId, status}` SignalR event after its status write (only when the message has a real `ThreadId`), same pattern as the existing `outboundMessageSent`/`threadAssigned` broadcasts. `useInboxHub.ts` gained a matching listener invalidating `["thread", threadId]`. `MockGatewayController.Send`'s earlier `Sent` transition deliberately doesn't broadcast (out of the plan's stated scope) — the single-tick state is rarely visible live as a result, only on a page load that happens to land mid-transition; flagged, not silently expanded beyond scope. `DELIVERY_STATUS_CONFIG` already had the correct Queued=clock/Sent=single-tick/Delivered=double-tick icon mapping — verified, not rebuilt. 160/160 fast backend tests still green; solution builds clean. Verified live end-to-end (not just read the code): a scripted SignalR client connected to `/hubs/inbox` with a real JWT, a reply was sent via `curl` against the running Docker stack, and both `outboundMessageSent` and `messageStatusUpdated {..., status:"Delivered"}` were observed arriving in order. |
| 2026-07-14 | 9 (P9-03) | New shared `components/v2/date-time-picker.tsx` (`DateTimePicker`) — Material-style date card (new shadcn `calendar` primitive, react-day-picker v10) + a custom pointer-driven clock-face time picker, one component swapped into every date/datetime input app-wide (`NewTaskForm`/`CreateTaskForm`, `new-conversation-dialog.tsx`, `conversation-panel.tsx`'s Schedule toggle, `TaskBoardPageV2.tsx`'s Due from/to filters, `AuditLogPageV2.tsx` + legacy `AuditLogPage.tsx`'s date range). Drop-in `value`/`onChange` string compatibility with the native inputs it replaced, so downstream `new Date(value).toISOString()` conversions were untouched. Hit and fixed a `shadcn add calendar` quirk in this repo: the CLI wrote files to a literal `@/` directory at the repo root instead of resolving the `src/` alias — moved by hand, stray directory removed, a duplicate default-template `button.tsx` it also wrote was diffed and discarded (never touched the real one). See `CODEBASE_MAP.md` §6g. `tsc -b`/`vite build` clean, `docker compose up -d --build web` verified serving. Flagged, not silently claimed: the clock face's pointer-angle interaction was **not** click-through verified in a real browser (no browser tool available this session) — highest-risk unverified piece in Step 9 so far. |
| 2026-07-14 | 9 (P9-05) | Dual-safety net for template edits reaching already-queued messages. `TemplatesController.Update` now sweeps every `Queued` `OutboundMessage` with a matching `TemplateId` and nulls `RenderedBody` when `Body` changes (net #1). Verified, not assumed, that net #2 (`MessageDispatcher.DispatchOneAsync`'s existing unconditional re-render on every dispatch attempt) already fully covers propagation on its own today, since every current production creation path leaves `RenderedBody` null — net #1 is kept anyway per Zohaib's explicit "handle at both ends" request, and would be the only net that mattered if a future creation path ever pre-rendered `RenderedBody`. Same InMemory-vs-real-provider `ExecuteUpdateAsync` branch as the existing `UnreadCount` atomic-increment fix. 1 new integration test (also proves non-`Queued` messages are untouched), 85/85 fast backend tests green. Verified live against the real Docker/MySQL stack via `curl` — the real-provider branch (untestable against InMemory) ran clean; the touched seed template's body was restored afterward. |
| 2026-07-14 | 9 (P9-04) | Composer template insertion now resolves real values instead of raw `{{field}}` tokens. New `GET api/threads/{id}/templates/{templateId}/preview` (`ThreadsController.PreviewTemplate`) resolves `{{patient_name}}` from the thread's real patient and `{{appointment_time}}` from the patient's next real `Scheduled` appointment (else a generated future dummy time), reusing the existing `TemplateRenderer.Render` unchanged — the DB-querying field resolution lives in the controller since `NotifyHub.Domain` has no EF dependency. `ConversationPanelV2.handleInsertTemplate` calls it and fills the editable composer textarea (falls back to the raw template body if the preview call fails). BR-013 unaffected: dispatch-time rendering still snapshots `RenderedBody` at actual send time regardless of what the preview showed. 2 new integration tests (dummy-time fallback, real-appointment resolution), 84/84 fast backend tests green. Verified live against the real Docker stack via `curl` — both resolution paths confirmed working with real seed data. |
