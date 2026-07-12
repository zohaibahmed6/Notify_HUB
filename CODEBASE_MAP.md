# NotifyHub — Codebase Map

Reality-first: what's actually implemented, with file:line citations. Not a restatement of
`PROJECT_CONTEXT.md`'s requirements — for intentions, deviations, and step-by-step history see
`PROJECT_CONTEXT.md` (spec) and `STATUS.md` (build log, deviations, known limitations). If this
file ever contradicts the code, the code wins — fix this file and flag the discrepancy.

Last verified against commit `1c6c47b` (step 6, committed and reviewed) plus step 7's
documentation additions (README, ADRs, security/AI-log docs, coverage report) and step 7's
fix rounds (CI dependency-vulnerability scan + vulnerable transitive package pins;
`WebhooksController.Inbound`'s `UnreadCount` increment made atomic) — none yet committed.

---

## 1. Solution structure

- `NotifyHub.Api/` — REST endpoints, SignalR hub, Swagger, auth, EF Core DbContext registration + startup migrate/seed.
- `NotifyHub.Worker/` — three `BackgroundService`s: message dispatcher, escalation job poller, reminder scheduler (see §4).
- `NotifyHub.Domain/` — entities, enums, pure business-rule logic (idempotency, backoff, recurrence, opt-out matching, due-date defaults). No EF/HTTP deps.
- `NotifyHub.Infrastructure/` — EF Core `DbContext` + Fluent API configs, `MessageDispatcher`, `EscalationJob`, seed steps.
- `NotifyHub.Tests/NotifyHub.Domain.Tests/` — xUnit unit tests, no DB.
- `NotifyHub.Tests/NotifyHub.Integration.Tests/` — xUnit integration tests, mostly EF Core InMemory, one real-MySQL test.
- `notifyhub-web/` — React 18 + Vite + TypeScript + TanStack Query + shadcn/ui + SignalR client.
- `notifyhub-web/e2e/` — Playwright end-to-end suite.

---

## 2. Data model (as implemented)

DbContext: `NotifyHub.Infrastructure/Persistence/NotifyHubDbContext.cs:6` — DbSets at lines 8-18.
Configs auto-discovered via `modelBuilder.ApplyConfigurationsFromAssembly(...)` at line 23 (no
manual per-entity registration) — every `IEntityTypeConfiguration<T>` in
`NotifyHub.Infrastructure/Persistence/Configurations/*.cs` applies automatically.

| Entity → table | Entity file | Config file | Key fields | Relationships / indexes |
|---|---|---|---|---|
| `User` → `users` | `NotifyHub.Domain/Entities/User.cs:5` | `UserConfiguration.cs:7` | Id, Username, PasswordHash, Role | Unique index `Username` (:18) |
| `RefreshToken` → `refresh_tokens` | `RefreshToken.cs:3` | `RefreshTokenConfiguration.cs:7` | Id, UserId, TokenHash, ExpiresAt, RevokedAt | Unique index `TokenHash` (:18); index `UserId` (:25); FK on `User` side |
| `Patient` → `patients` | `Patient.cs:4` | `PatientConfiguration.cs:7` | Id, Name, Phone, OptOutAt | Unique index `Phone` (:22) |
| `Appointment` → `appointments` (stub) | `Appointment.cs:6` | `AppointmentConfiguration.cs:7` | Id, PatientId, ScheduledAt, Status | FK `Patient`, cascade delete (:22-25); index `PatientId` (:27) |
| `MessageTemplate` → `message_templates` | `MessageTemplate.cs:5` | `MessageTemplateConfiguration.cs:7` | Id, Name, Body (≤1000), TriggerType, OffsetHours | No indexes |
| `OutboundMessage` → `outbound_messages` | `OutboundMessage.cs:5` | `OutboundMessageConfiguration.cs:7` | Id, PatientId, ThreadId?, TemplateId?, SenderType, TriggerReference?, RenderedBody?, Status, IdempotencyKey?, AttemptCount, NextRetryAt? | Unique index `IdempotencyKey` (:32); FKs Patient/Template/Thread all `Restrict` (:36-49); composite index `(Status, NextRetryAt)` (:52); composite index `(ThreadId, CreatedAt)` (:53) |
| `DeliveryStatusHistory` → `delivery_status_history` | `DeliveryStatusHistory.cs:5` | `DeliveryStatusHistoryConfiguration.cs:7` | Id, MessageId, Status, OccurredAt | FK `Message`, cascade delete (:22-25); index `MessageId` (:27) |
| `AuditLog` → `audit_log` | `AuditLog.cs:4` | `AuditLogConfiguration.cs:7` | Id, Actor, Action, EntityType, EntityId, OccurredAt, Detail? | Composite index `(EntityType, EntityId)` (:20); index `Actor` (:21); no FK (polymorphic ref) |
| `ConversationThread` → `threads` | `ConversationThread.cs:7` | `ConversationThreadConfiguration.cs:7` | Id, PatientId, AssignedStaffId?, UnreadCount | **Unique index `PatientId`** (:21 — the race-safety guarantee, see §5); FK `Patient` `Restrict` (:17-20); FK `AssignedStaff` `Restrict` (:23-26); index `AssignedStaffId` (:29) |
| `InboundMessage` → `inbound_messages` | `InboundMessage.cs:4` | `InboundMessageConfiguration.cs:7` | Id, ThreadId, Body (≤1000), ReceivedAt | FK `Thread`, cascade delete (:18-21); composite index `(ThreadId, ReceivedAt)` (:24) |
| `TaskItem` → `tasks` | `TaskItem.cs:7` | `TaskItemConfiguration.cs:7` | Id, ThreadId, Priority, DueAt, Status, AssignedStaffId?, OriginalOwnerId, IsRecurring, RecurrenceIntervalDays?, RecurrenceEndDate?, RecurrenceMaxOccurrences?, OccurrenceCount | FK `Thread` cascade (:29-32); FK `AssignedStaff`/`OriginalOwner` `Restrict` (:34-42); composite index `(Status, DueAt)` (:45, drives escalation job); index `AssignedStaffId` (:46) |

Enums: `UserRole` (`Enums/UserRole.cs:3`), `MessageStatus` (`:3`, Queued/Sending/Sent/Delivered/Failed/**Superseded** — 6th value added in step 5, terminal, set only by `ReminderScheduler` on a rescheduled appointment's stale queued reminder, BR-010; never picked up by `MessageDispatcher`'s `Status == Queued` query so nothing else needed to change),
`TriggerType` (`:3`, AppointmentReminder/MedicationAlert/PrescriptionAlert), `SenderType` (`:3`,
System/Staff), `AppointmentStatus` (`:4`), `NotifyHubTaskStatus` (`:6`, Open/InProgress/Completed/
Escalated/**Cancelled** — 5th value added per BR-007b, see STATUS.md), `TaskPriority` (`:3`, Low/
Medium/High/Urgent).

---

## 3. API endpoints (as implemented)

Default auth policy: every endpoint requires authentication unless marked otherwise — global
`AuthorizeFilter` (`NotifyHub.Api/Extensions/AuthServiceCollectionExtensions.cs:65-69`), no
role-based fallback policy, so any authenticated user (Admin or Staff) passes unless a controller/
action opts in to something stricter.

### `AuthController` — `NotifyHub.Api/Controllers/AuthController.cs` (`[Route("api/auth")]` :19)
| Verb + route | Method:line | Auth |
|---|---|---|
| POST `api/auth/login` | `Login` :28 | `[AllowAnonymous]` |
| POST `api/auth/refresh` | `Refresh` :48 | `[AllowAnonymous]` (httpOnly cookie sent automatically) |
| POST `api/auth/logout` | `Logout` :81 | `[AllowAnonymous]` |
| GET `api/auth/me` | `Me` :102 | default authenticated policy |
| GET `api/auth/admin-only` | `AdminOnly` :113 | `[Authorize(Roles = "Admin")]` |

Refresh-token cookie name `notifyhub_refresh` (:26); set/clear in `SetRefreshCookie` (:163-173) /
`DeleteRefreshCookie` (:175-178); issuance in `IssueTokensAsync` (:117-161).

### `ThreadsController` — `NotifyHub.Api/Controllers/ThreadsController.cs` (`[Route("api/threads")]` :20)
| Verb + route | Method:line | Auth | Notes |
|---|---|---|---|
| GET `api/threads` | `List` :23-41 | default authenticated | paginated |
| GET `api/threads/{id}` | `Detail` :43-74 | default authenticated | resets `UnreadCount = 0` on open (:58-60); messages paginated via `GetMessagesPageAsync` (:89-132, step 6/FR-010 — see §5) instead of the old unpaginated `.Include(InboundMessages).Include(OutboundMessages)` |
| POST `api/threads/{id}/messages` | `Reply` :139 | default authenticated | BR-001b opt-out check (:145-148); broadcasts `outboundMessageSent` (:169) |
| POST `api/threads/{id}/assign` | `Assign` :177 | default authenticated | self-assign OK; assigning others requires caller role Admin, else 403 (:190-191); broadcasts `threadAssigned` (:203) |
| POST `api/threads/{id}/tasks` | `CreateTask` :209 | default authenticated | |

### `TasksController` — `NotifyHub.Api/Controllers/TasksController.cs` (`[Route("api/tasks")]` :17)
| Verb + route | Method:line | Auth | Notes |
|---|---|---|---|
| GET `api/tasks` | `List` :19 | default authenticated | |
| GET `api/tasks/{id}` | `Detail` :51 | default authenticated | BR-014 auto-revert if opened by assignee (:58-64) |
| PATCH `api/tasks/{id}` | `Update` :69 | default authenticated | BR-014 auto-revert on assignee action (:119-124); recurrence spawn via `SpawnNextOccurrenceIfDue` (:133-159) |

### `TemplatesController` — `NotifyHub.Api/Controllers/TemplatesController.cs` (`[Route("api/templates")]` :14)
| Verb + route | Method:line | Auth |
|---|---|---|
| GET `api/templates` | `List` :17 | default authenticated |
| POST `api/templates` | `Create` :35 | default authenticated |
| PATCH `api/templates/{id}` | `Update` :59-89 | default authenticated |

Applies only non-null fields from `UpdateTemplateRequest` (`NotifyHub.Api/Templates/Dtos/UpdateTemplateRequest.cs`) — added step 6 to close §6b's "create/edit" gap (only `GET`/`POST` existed before).

### `AuditController` — `NotifyHub.Api/Controllers/AuditController.cs` (`[Route("api/audit")]` :17-19, step 6/FR-011)
| Verb + route | Method:line | Auth | Notes |
|---|---|---|---|
| GET `api/audit` | `List` :21-27 | `[Authorize(Roles="Admin")]` (:22) — first non-default, non-webhook auth policy in the codebase | filters `actor`/`action`/`from`/`to`, paginated via `PagedResult<T>.Clamp` |
| GET `api/audit/mine` | `Mine` :29-35 | default authenticated | same filters minus `actor` — server hardcodes `actor` to the caller's own username (:33), ignoring any client value |

Shared query logic: `QueryAsync` (:37-63).

### `WebhooksController` — `NotifyHub.Api/Controllers/WebhooksController.cs` (`[Route("api/webhooks")]` :18, class-level `[AllowAnonymous][SharedSecret]` :19-20)
| Verb + route | Method:line | Notes |
|---|---|---|
| POST `api/webhooks/gateway-receipt` | `GatewayReceipt` :26 | shared-secret only |
| POST `api/webhooks/inbound` | `Inbound` :81 | shared-secret only; broadcasts `inboundMessageReceived` (:108-114) |

Race-safe find-or-create: `FindOrCreateThreadAsync` (:119-141) — see §5.

### `MockGatewayController` — `NotifyHub.Api/Controllers/MockGatewayController.cs` (`[Route("api/mock-gateway")]` :19, class-level `[AllowAnonymous][SharedSecret]` :20-21)
| Verb + route | Method:line | Notes |
|---|---|---|
| POST `api/mock-gateway/send` | `Send` :28 | called by Worker dispatcher (service-to-service); posts back to `api/webhooks/gateway-receipt` via named HttpClient `"self"` (:57-63) |

### `SharedSecretAttribute` — `NotifyHub.Api/Webhooks/SharedSecretAttribute.cs:12`
`IAsyncActionFilter`; header `X-Webhook-Secret` (:14); constant-time compare via
`CryptographicOperations.FixedTimeEquals` (:27); 401 on mismatch (:29).

### SignalR — `InboxHub` at `/hubs/inbox`
- Hub class: `NotifyHub.Api/Inbox/InboxHub.cs:10` — empty body, server→client push only (no client-invocable methods).
- Mapped: `NotifyHub.Api/Program.cs:126`.
- Auth: JWT via `?access_token=` query string for `/hubs/*` only (browsers can't set WS headers) — `AuthServiceCollectionExtensions.cs:47-60`.
- Broadcast model: `Clients.All` (flat, no per-staff filtering).

| Event | Emitted from | Payload |
|---|---|---|
| `inboundMessageReceived` | `WebhooksController.cs:108-114` | `{ threadId, patientId, body, receivedAt }` |
| `outboundMessageSent` | `ThreadsController.cs:127` | `{ threadId }` |
| `threadAssigned` | `ThreadsController.cs:161` | `{ threadId, assignedStaffId }` |

---

## 4. Background jobs

| Job | File:line | Trigger | What it does |
|---|---|---|---|
| `DispatcherWorker` | `NotifyHub.Worker/DispatcherWorker.cs:8-35` | Fixed 5s poll loop (:10, hardcoded, not config-driven); 5s error-retry delay (:11) | Resolves `MessageDispatcher` per scope, calls `DispatchDueMessagesAsync` (:22) |
| `MessageDispatcher` | `NotifyHub.Infrastructure/Messaging/MessageDispatcher.cs` | Called by `DispatcherWorker` | `DispatchDueMessagesAsync` (:19-35): batch of 10 `Queued` messages due now, ordered by `CreatedAt`. `DispatchOneAsync` (:37-90): opt-out short-circuit (:42-50), renders template if set (:54-59), POSTs to mock gateway (:66-67), on failure increments attempt count and either terminalizes via `RetryBackoffPolicy.IsTerminal` (:77-81) or requeues with backoff (:83-86). `RenderAsync` (:92-113) parses `TriggerReference` for `{{appointment_time}}` (:101-109). |
| `EscalationWorker` | `NotifyHub.Worker/EscalationWorker.cs:8-36` | Config-driven poll, `Escalation:PollIntervalSeconds` default 60s (:17); 5s error-retry delay (:13) | Resolves `EscalationJob` per scope, calls `EscalateOverdueTasksAsync` (:26) |
| `EscalationJob` | `NotifyHub.Infrastructure/Escalation/EscalationJob.cs` | Called by `EscalationWorker` | `EscalateOverdueTasksAsync` (:19-61): batch of 100 overdue non-terminal tasks (:23-29), resolves lowest-id Admin as fallback (:36-40), sets `Escalated` + audits (:45-47), reassigns + audits "auto-reassigned" if not already assigned to that admin (:49-54) |
| `ReminderWorker` | `NotifyHub.Worker/ReminderWorker.cs:8-36` | Config-driven poll, `Reminders:PollIntervalSeconds` default 900s/15min (:17, locked decision per §14); 5s error-retry delay (:13) | Resolves `ReminderScheduler` per scope, calls `RunAsync` (:26) |
| `ReminderScheduler` | `NotifyHub.Infrastructure/Reminders/ReminderScheduler.cs` | Called by `ReminderWorker` | `RunAsync` (:18-34): loads `AppointmentReminder` templates, then two passes. `SupersedeStaleRemindersAsync` (:41-86, BR-010): finds `Queued` messages tied to a reminder template, parses each `TriggerReference` via `ReminderTriggerReference.TryParse`, and marks `Superseded` any whose embedded `ScheduledAt` no longer matches the appointment's current value (rescheduled or deleted) — audits "superseded". `CreateDueRemindersAsync` (:91-142, FR-009/BR-003): for each upcoming `Scheduled` appointment × reminder template where `ReminderDueCalculator.IsDue` is true, checks `outbound_messages.idempotency_key` first (skip if exists — re-run safe) then queues a new message, `ThreadId=null`/`RenderedBody=null` (rendered later by the existing `MessageDispatcher.RenderAsync`, unchanged). |

**FR-009 reminder scheduler implemented in step 5** (`ReminderWorker`/`ReminderScheduler`, above) —
registered in `NotifyHub.Worker/Program.cs` alongside `DispatcherWorker`/`EscalationWorker`. No
appointment-management endpoint exists (appointments are stub data, §7/out of scope for a dedicated
screen), so BR-010's reschedule-supersede logic is poll-based, not event-driven — see §5 and
STATUS.md's deviations for the tradeoff.

---

## 4a. Seed steps (`NotifyHub.Infrastructure/Seed/`, run in DI-registration order by `DbSeedRunner`)

All registered as `IDbSeedStep` in `NotifyHub.Api/Program.cs` (:55-61) and run unconditionally at
Api startup (`Program.cs` :105-106), including in every integration test that boots the Api
pipeline — no environment gating. Order: `UserSeedStep` → `SecondStaffSeedStep` →
`PatientAppointmentSeedStep` (10 demo patients+appointments) → `TemplateSeedStep` (4 templates) →
`DemoOutboundMessageSeedStep` (5 demo messages) → `PerformanceSeedStep` (step 6, FR-010).

`PerformanceSeedStep` (`NotifyHub.Infrastructure/Seed/PerformanceSeedStep.cs:31-151`) — constructor
parameter `targetMessageCount` (default 50,000), read from config key `Seed:PerformanceMessageCount`
in `Program.cs`'s DI registration. `RunAsync` (:40-82): idempotency check via a patient-name marker
prefix (:42, independent of `DemoOutboundMessageSeedStep`'s own "any message exists" check), thread
count scales with message target (~50 messages/thread, clamped 10-1,000, :52), 90/10
outbound/inbound split (`OutboundRatio`, :38), all outbound messages get a terminal status
(Delivered/Failed, :106 — never `Queued`, so `DispatcherWorker` never picks any of them up), batched
inserts via `SeedOutboundMessagesAsync`/`SeedInboundMessagesAsync`/`FlushAsync` (:84-151, chunks of
2,000, `AutoDetectChangesEnabled=false` during the loop).

**Test-factory overrides**: `Seed:PerformanceMessageCount` is capped to 50
(`CustomWebApplicationFactory.cs`) / 100 (`MySqlWebApplicationFactory.cs`) — otherwise every
integration test booting the Api pipeline would also seed 50,000 rows per fixture.
`PerformanceSeedStepTests` deliberately uses its own isolated `NotifyHubDbContext` instead of either
factory, to test the step's own idempotency without colliding with the automatic startup seed.

---

## 5. Key business logic (pure/testable, Domain layer unless noted)

| Rule | File:line | Computes |
|---|---|---|
| Idempotency key generation | `NotifyHub.Domain/Messaging/IdempotencyKeyGenerator.cs:9-17` | SHA-256 hex of `"{patientId}:{templateId}:{triggerReference}"` |
| Retry/backoff calculation (BR-011) | `NotifyHub.Domain/Messaging/RetryBackoffPolicy.cs:6-31` | `MaxAttempts=6`; delays 1/2/4/8/16 min indexed by `attemptCount-1`; `IsTerminal` at `attemptCount>=6` |
| Recurrence calculation (BR-007) | `NotifyHub.Domain/Tasks/RecurrenceCalculator.cs:6-31` | `NextOccurrence`: due-date-anchored (`previousDueAt + intervalDays`, no drift); `null` if past `recurrenceEndDate` or over `recurrenceMaxOccurrences` |
| Opt-out keyword matching (FR-006) | `NotifyHub.Domain/Inbox/OptOutKeywordMatcher.cs:6-12` | Case-insensitive **exact** (trimmed) match against STOP/UNSUBSCRIBE/CANCEL/END/QUIT — not substring |
| Task due-date defaults (FR-008) | `NotifyHub.Domain/Tasks/TaskDueDateDefaults.cs:6-16` | Urgent+4h / High+1d / Medium+3d / Low+7d from creation |
| BR-014 escalation auto-revert | `NotifyHub.Api/Controllers/TasksController.cs` — two call sites: `Detail` :58-64 (on open by assignee), `Update` :119-124 (on any action by assignee that doesn't itself set a new status) | Flips `Escalated` → `InProgress` |
| Race-safe thread creation | `NotifyHub.Api/Controllers/WebhooksController.cs:119-141` (`FindOrCreateThreadAsync`) | Optimistic insert, `catch (DbUpdateException)` on the unique index (`ConversationThreadConfiguration.cs:21`), detach + re-read the winner (:138-139). **Now covered by a real-MySQL test** — `NotifyHub.Tests/NotifyHub.Integration.Tests/InboundWebhookThreadRaceMySqlTests.cs` (see §7); EF Core InMemory (used by every other integration test) can't reproduce genuine connection-level locking, so this was previously untested at the actual race. |
| Reminder due-window calculation (FR-009) | `NotifyHub.Domain/Messaging/ReminderDueCalculator.cs:9-10` (`IsDue`) | `now < scheduledAt && now >= scheduledAt.AddHours(-offsetHours)` — true once the offset window opens, false once the appointment has occurred |
| Reminder trigger-reference build/parse (BR-009/BR-010) | `NotifyHub.Domain/Messaging/ReminderTriggerReference.cs:16-17` (`Build`), `:21-38` (`TryParse`) | Format `appointment:{appointmentId}:reminder:{offsetHours}h:{scheduledAt.Ticks}` — embeds `ScheduledAt` itself (not a version counter, see STATUS.md deviations) so a reschedule always yields a new reference; `TryParse` rejects non-reminder formats (e.g. seed data's `appointment:{id}:created`) |
| Reminder scheduling + reschedule-supersede (FR-009/BR-003/BR-010) | `NotifyHub.Infrastructure/Reminders/ReminderScheduler.cs` (see §4) | Poll-based supersede-then-create, reusing `outbound_messages`/`IdempotencyKeyGenerator`/`MessageDispatcher` unchanged |
| Thread message-history pagination (FR-010) | `NotifyHub.Api/Controllers/ThreadsController.cs:89-132` (`GetMessagesPageAsync`) | Merge-paginates `inbound_messages`/`outbound_messages` (independently ordered by `ReceivedAt`/`CreatedAt`) without ever loading a thread's full history: pulls only `skip+pageSize` rows DESC from each table (provably sufficient — see the method's doc comment, :76-88), merges in memory, slices to the requested page, re-sorts ascending for chat reading order. Page 1 = most recent messages. |

**Fixed** (was logged in STATUS.md's Final review checklist as an open item): the `Inbound`
action's `thread.UnreadCount` increment (`WebhooksController.cs:104-118`, after
`FindOrCreateThreadAsync` returns) was confirmed to be read-then-write on a tracked entity — a
genuine lost-update race under concurrent inbound webhooks for the same thread, same race category
as the (already-fixed) thread-duplication bug. Fixed via EF Core's atomic `ExecuteUpdateAsync`:
`db.Threads.Where(t => t.Id == thread.Id).ExecuteUpdateAsync(s => s.SetProperty(t => t.UnreadCount, t => t.UnreadCount + 1), ct)`
— a single `UPDATE ... SET UnreadCount = UnreadCount + 1` per request, no read-modify-write window
— for real database providers. The InMemory provider (used by the fast test suite) can't translate
`ExecuteUpdate`'s `SetProperty` (throws `InvalidOperationException`), so the action branches on
`db.Database.ProviderName` and keeps the old tracked increment there — safe, since InMemory tests
are single-request and never exercise the race. Proven atomic by
`InboundWebhookThreadRaceMySqlTests.ConcurrentInbound_ForExistingThread_IncrementsUnreadCountExactlyN`
(real MySQL, 30 concurrent requests against one pre-existing thread, asserts `UnreadCount` lands at
exactly 31).

**Second known risk, found in step 6 — fixed same step**: `ThreadsController.Detail` used to load
a thread's entire inbound+outbound message history unpaginated via `.Include()`. Flagged during
step 6's 50k-seed design as an FR-010 violation, then fixed before step 6 was considered done (see
the row above and `ThreadsControllerTests.Detail_PaginatesMessages_DoesNotReturnFullHistory` in §7)
rather than deferred — no longer an open item.

---

## 6. Frontend structure (`notifyhub-web/src/`)

**Pages** (`src/pages/`): `LoginPage.tsx` (auth entry), `InboxPage.tsx` (thread list + `ConversationPanel`), `TaskBoardPage.tsx` (status-filtered task list + `NewTaskForm`/`TaskDetailPanel`), `TemplatesPage.tsx` (§6b, step 6 — list + create form + inline per-row edit form via a shared `TemplateForm` component defined in the same file), `AuditLogPage.tsx` (§6b, step 6 — role-branches on `user.role`: Admin gets an actor filter + `/api/audit`, Staff gets `/api/audit/mine`; action/date-range filters, paginated table, empty state).

**Components**:
- `components/layout/AppShell.tsx` — top nav; mounts the single shared `useInboxHub()` connection (:18). `NAV_LINKS` (:8-13) now includes Templates/Audit log alongside Inbox/Task board.
- `components/inbox/ConversationPanel.tsx` — merged inbound/outbound view, reply, assign, auto-scroll-if-at-bottom. Messages are paginated server-side (step 6/FR-010): `useThread` only returns page 1 (most recent); local `olderMessages` state (:26) accumulates additional pages fetched directly via `apiClient` (bypassing TanStack Query's cache, since this is an append-only local scrollback) when the "Load earlier messages" button (:146-152, shown while `hasMoreOlder`) is clicked.
- `components/inbox/CreateTaskForm.tsx` — inline "make task" form.
- `components/tasks/NewTaskForm.tsx` — thread-picker + priority + due date (no standalone task endpoint).
- `components/tasks/TaskDetailPanel.tsx` — fetching via `useTask(id)` (:12-19) is what triggers BR-014's server-side revert.
- `components/PriorityBadge.tsx`, `components/TaskStatusBadge.tsx` — color+label badges.
- `components/ui/*` — shadcn primitives (generated).

**Hooks** (`src/hooks/`):
- `useThreads.ts`: `useThreads()` :7 (list), `useThread(id)` :14 (detail — `messages` is now `PagedResult<ThreadMessageDto>`, page 1 only, step 6/FR-010; invalidates `["threads"]` since opening resets unread), `useReplyMutation` :30, `useAssignMutation` :41, `useCreateTaskMutation` :53.
- `useTasks.ts`: `useTasks(status?)` :7, `useTask(id)` :21 (triggers BR-014 revert), `useUpdateTaskMutation()` :29.
- `useInboxHub.ts`: `useInboxHub()` :20 — owns SignalR connection lifecycle + query invalidation.
- `useAudit.ts` (step 6): `useAuditLog(isAdmin, filters)` — picks `/api/audit` vs `/api/audit/mine` based on `isAdmin`, builds the query string from `actor`/`action`/`from`/`to`/`page`/`pageSize`.
- `useTemplates.ts` (step 6): `useTemplates()` (list), `useCreateTemplateMutation()`, `useUpdateTemplateMutation()` (PATCH, invalidates `["templates"]`).

**Auth wiring**:
- `context/AuthContext.tsx` — silent refresh-on-mount effect :41-57 (posts `/api/auth/refresh` with `skipAuth`, httpOnly cookie sent automatically); listens for `"auth:logout"` window event :36-38.
- `lib/tokenStore.ts:14` — in-memory singleton (`let tokens`), read via `getAccessToken()` :26-28.
- `routes/ProtectedRoute.tsx:11-17` — renders `null` while bootstrapping, redirects to `/login` if unauthenticated.

**SignalR wiring**:
- `lib/signalr.ts:9-14` — `createInboxConnection()`: hub URL `${BASE_URL}/hubs/inbox` (:11), JWT via `accessTokenFactory` (:12), `.withAutomaticReconnect()` (:14).
- `hooks/useInboxHub.ts` — connection created/started :24/:41, stopped on unmount :46. Listeners: `inboundMessageReceived` :26, `threadAssigned` :31, `outboundMessageSent` :36 — all invalidate `["threads"]`/`["thread", threadId]`.

**API client**: `lib/apiClient.ts` — JWT attached :46-49; 401 handling :53-68 (de-dupes concurrent refreshes via shared `refreshPromise` :22/:54-58, retries once, else clears token store + dispatches `"auth:logout"` :65-67). Base URL derivation: `lib/apiBaseUrl.ts:9-10` (`${protocol}//${hostname}:5000`, `VITE_API_URL` override available).

**Routing**: `main.tsx:17-21` mounts `BrowserRouter`. Table in `App.tsx:13-26`: `/login` public (:14); `/`→`/inbox` redirect (:17), `/inbox`/`/tasks`/`/templates`/`/audit` (:18-21, last two added step 6) all under `<ProtectedRoute>`+`<AppShell>` (:15-16); `*`→`/` (:24).

---

## 7. Test structure

### Domain (`NotifyHub.Tests/NotifyHub.Domain.Tests/`) — no DB
Files: `PasswordPolicyTests.cs`, `TemplateRendererTests.cs`, `IdempotencyKeyGeneratorTests.cs`, `RetryBackoffPolicyTests.cs`, `OptOutKeywordMatcherTests.cs`, `TaskDueDateDefaultsTests.cs`, `RecurrenceCalculatorTests.cs`, `ReminderDueCalculatorTests.cs`, `ReminderTriggerReferenceTests.cs`.
Run: `dotnet test NotifyHub.Tests/NotifyHub.Domain.Tests`

### Integration (`NotifyHub.Tests/NotifyHub.Integration.Tests/`)
| Test file | Factory |
|---|---|
| `AuthEndpointTests.cs`, `EscalationJobTests.cs`, `InboundWebhookTests.cs`, `MessageDispatcherOptOutTests.cs`, `TasksControllerTests.cs`, `ThreadsControllerTests.cs`, `ReminderSchedulerTests.cs`, `AuditControllerTests.cs`, `TemplatesControllerTests.cs` | `CustomWebApplicationFactory` (EF Core InMemory) |
| `OutboundPipelineTests.cs` | `ReliableGatewayWebApplicationFactory` (happy path) / `FailingGatewayWebApplicationFactory` (retry) — both subclass `CustomWebApplicationFactory` |
| `InboundWebhookThreadRaceMySqlTests.cs` | `MySqlWebApplicationFactory` — real MySQL, `[Trait("Category","MySql")]`, exercises `FindOrCreateThreadAsync`'s race guard under genuine concurrent connections |
| `PerformanceSeedStepTests.cs` | **No factory** — deliberately builds its own isolated `NotifyHubDbContext` (`UseInMemoryDatabase` with a fresh GUID name) instead of using `CustomWebApplicationFactory`, since that factory's automatic startup seeding (with `PerformanceSeedStep` registered as a real `IDbSeedStep`) would trip this step's own idempotency marker before the test calls `RunAsync` explicitly |

`ThreadsControllerTests.Detail_PaginatesMessages_DoesNotReturnFullHistory` (step 6/FR-010): seeds 60
messages on one thread, asserts page 1 returns exactly 25 (not the full 60) and that pages 1+2
combined reconstruct the correct most-recent-50 in chronological order with zero overlap —
verifies `ThreadsController.GetMessagesPageAsync`'s merge-pagination correctness end-to-end.

Run the fast (InMemory-only) suite: `dotnet test NotifyHub.Tests/NotifyHub.Integration.Tests --filter "Category!=MySql"`
Run the MySQL-only test (needs `docker compose up -d mysql` locally, or CI's `mysql` service): `dotnet test NotifyHub.Tests/NotifyHub.Integration.Tests --filter "Category=MySql"`
Run everything (what CI does, no filter): `dotnet test NotifyHub.sln`

### Playwright e2e (`notifyhub-web/e2e/`)
Specs: `auth.spec.ts`, `optout.spec.ts`, `unread.spec.ts`, `assignment.spec.ts`, `tasks.spec.ts`, `reply.spec.ts`, `realtime.spec.ts` (+ `helpers.ts` shared fixtures). 11 tests total.
Config: `notifyhub-web/playwright.config.ts` — `testDir: "./e2e"`, no `webServer` auto-start (needs the full `docker-compose` stack already running).
Run: `docker-compose up -d`, then `cd notifyhub-web && npm run test:e2e` (or the Docker-image-based invocation documented in STATUS.md's "Playwright e2e suite" section, used when local node/npm aren't available).

---

## 8a. Documentation (step 7, FR-012/013/014/015/016/017/018/019)

- `README.md` — project overview, one-command run, screens, test/coverage commands, documentation index.
- `docs/adr/0001-outbound-queue.md`, `0002-dispatcher-hosting.md`, `0003-rbac-model.md` (FR-016).
- `docs/SECURITY.md` (FR-018) — OWASP Top-10 self-assessment + sub-criteria (a)-(e), citing the actual auth/validation/EF-parameterization/secrets code.
- `docs/AI_USAGE_LOG.md` (FR-019).
- `docs/coverage/DOMAIN_COVERAGE.md` (FR-013) — methodology + per-class breakdown for the measured 94.2% line-coverage figure on `NotifyHub.Domain` (both `Domain.Tests` and `Integration.Tests` runs merged via `dotnet-reportgenerator-globaltool`, filtered to `NotifyHub.Domain.*`).

## 8. Known limitations / deviations

See `STATUS.md`:
- "Documented deviations from PROJECT_CONTEXT.md" — `Cancelled` task status, ad-hoc replies through the dispatcher pipeline, "Blocked" audit action, thread-assignment target validation, escalation fallback Admin selection, escalation poll interval, task-board reassign scope, task creation requiring a thread first, reminder `trigger_reference` format (ticks vs. version counter), reschedule-supersede being poll-based not event-driven (no appointment-management endpoint exists), `GET /api/audit` being the first Admin-only (not default-authenticated) endpoint, `PATCH /api/templates/{id}` added beyond the literal step-6 work-item list, the 50k seed's thread-spread/status-mix design choices, `PerformanceSeedStep`'s config-driven test-factory caps.
- "Known limitations (by design, not bugs)" — SignalR broadcasts to all sessions, no stale-"Sending" recovery sweep, `{{appointment_time}}` resolution, Worker not gating on Api migration, no frontend unit test suite.
- "Final review checklist" — both items are now closed: the `UnreadCount` atomicity question (§5 above) was confirmed to be a real read-then-write race and fixed with `ExecuteUpdateAsync`; the `ThreadsController.Detail` unpaginated-message-history item (also §5) was found and fixed within step 6. Nothing remains open.
