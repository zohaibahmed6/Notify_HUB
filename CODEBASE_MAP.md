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
| `User` → `users` | `NotifyHub.Domain/Entities/User.cs:5` | `UserConfiguration.cs:7` | Id, Username, PasswordHash, Role, **FullName?, Status** (added, see below) | Unique index `Username` (:18) |
| `RefreshToken` → `refresh_tokens` | `RefreshToken.cs:3` | `RefreshTokenConfiguration.cs:7` | Id, UserId, TokenHash, ExpiresAt, RevokedAt | Unique index `TokenHash` (:18); index `UserId` (:25); FK on `User` side |
| `Patient` → `patients` | `Patient.cs:4` | `PatientConfiguration.cs:7` | Id, Name, Phone, OptOutAt | Unique index `Phone` (:22) |
| `Appointment` → `appointments` (stub) | `Appointment.cs:6` | `AppointmentConfiguration.cs:7` | Id, PatientId, ScheduledAt, Status | FK `Patient`, cascade delete (:22-25); index `PatientId` (:27) |
| `MessageTemplate` → `message_templates` | `MessageTemplate.cs:5` | `MessageTemplateConfiguration.cs:7` | Id, Name, Body (≤1000), TriggerType, OffsetHours | No indexes |
| `OutboundMessage` → `outbound_messages` | `OutboundMessage.cs:5` | `OutboundMessageConfiguration.cs:7` | Id, PatientId, ThreadId?, TemplateId?, SenderType, TriggerReference?, RenderedBody?, Status, IdempotencyKey?, AttemptCount, NextRetryAt? | Unique index `IdempotencyKey` (:32); FKs Patient/Template/Thread all `Restrict` (:36-49); composite index `(Status, NextRetryAt)` (:52); composite index `(ThreadId, CreatedAt)` (:53) |
| `DeliveryStatusHistory` → `delivery_status_history` | `DeliveryStatusHistory.cs:5` | `DeliveryStatusHistoryConfiguration.cs:7` | Id, MessageId, Status, OccurredAt | FK `Message`, cascade delete (:22-25); index `MessageId` (:27) |
| `AuditLog` → `audit_log` | `AuditLog.cs:4` | `AuditLogConfiguration.cs:7` | Id, Actor, Action, EntityType, EntityId, OccurredAt, Detail? | Composite index `(EntityType, EntityId)` (:20); index `Actor` (:21); no FK (polymorphic ref) |
| `ConversationThread` → `threads` | `ConversationThread.cs:7` | `ConversationThreadConfiguration.cs:7` | Id, PatientId, AssignedStaffId?, UnreadCount | **Unique index `PatientId`** (:21 — the race-safety guarantee, see §5); FK `Patient` `Restrict` (:17-20); FK `AssignedStaff` `Restrict` (:23-26); index `AssignedStaffId` (:29) |
| `InboundMessage` → `inbound_messages` | `InboundMessage.cs:4` | `InboundMessageConfiguration.cs:7` | Id, ThreadId, Body (≤1000), ReceivedAt | FK `Thread`, cascade delete (:18-21); composite index `(ThreadId, ReceivedAt)` (:24) |
| `TaskItem` → `tasks` | `TaskItem.cs:7` | `TaskItemConfiguration.cs:7` | Id, ThreadId, Priority, DueAt, Status, AssignedStaffId?, OriginalOwnerId, IsRecurring, RecurrenceIntervalDays?, RecurrenceEndDate?, RecurrenceMaxOccurrences?, OccurrenceCount, **Description? (≤1000), TaskType, IsActive** (added — see below) | FK `Thread` cascade (:29-32); FK `AssignedStaff`/`OriginalOwner` `Restrict` (:34-42); composite index `(Status, DueAt)` (:45, drives escalation job); index `AssignedStaffId` (:46) |

**Schema additions (this feature set, increment 1)**: `TaskItem.Description` (string?, ≤1000 chars,
auto-populated from the thread's last message at creation — see §3's `ThreadsController.CreateTask`
once increment 5 lands), `TaskItem.TaskType` (new enum, see below, required, default `General`),
`TaskItem.IsActive` (bool, default `true` — a list-filter flag only, independent of the workflow
`Status` column; does not gate escalation/recurrence/forwarding). `User.FullName` (string?, display
name distinct from login `Username`), `User.Status` (new enum `UserStatus`: Active/Inactive/OnLeave,
default `Active`). Migration `20260713171136_AddTaskAndUserFields` — note the generated
`AddColumn` defaults were hand-corrected post-generation (`IsActive` → `true`, `Status`/`TaskType`
→ `"Active"`/`"General"` instead of EF's generated `false`/`""`, since `""` isn't a valid enum
member and would fail to deserialize on read for pre-existing rows).

Enums: `UserRole` (`Enums/UserRole.cs:3`), **`UserStatus`** (`Enums/UserStatus.cs:3`,
Active/Inactive/OnLeave), **`TaskType`** (`Enums/TaskType.cs:3`, RepeatRx/Recall/
AppointmentBooking/FollowUp/Finance/General/ClinicalReview/Administrative/Other), `MessageStatus` (`:3`, Queued/Sending/Sent/Delivered/Failed/**Superseded** — 6th value added in step 5, terminal, set only by `ReminderScheduler` on a rescheduled appointment's stale queued reminder, BR-010; never picked up by `MessageDispatcher`'s `Status == Queued` query so nothing else needed to change),
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

### `UsersController` — `NotifyHub.Api/Controllers/UsersController.cs` (`[Route("api/users")]`, added this feature set/increment 2)
| Verb + route | Auth | Notes |
|---|---|---|
| GET `api/users` | `[Authorize(Roles="Admin")]` | filters `role`/`status`, paginated |
| GET `api/users/assignable` | default authenticated | returns `Status == Active` users only — the source every assignee-picker in the app should use (replaces the frontend's earlier dedupe-from-already-fetched-lists workaround) |
| POST `api/users` | `[Authorize(Roles="Admin")]` | creates a user (`PasswordPolicy`/`IPasswordHasher<User>`, same as `UserSeedStep`); 409 on duplicate username |
| PATCH `api/users/{id}/status` | `[Authorize(Roles="Admin")]` | sets `User.Status`; transitioning **to** Inactive/OnLeave auto-forwards that user's non-terminal tasks (`Status` not in `{Completed,Cancelled}`) to a fallback Active Admin in the **same `SaveChangesAsync`**, audits each (`action:"forward"`, actor `"system"`), broadcasts `taskAssignmentChanged` per forwarded task |

`FallbackUserResolver.ResolveFallbackAdminIdAsync` (`NotifyHub.Infrastructure/Users/FallbackUserResolver.cs`)
— extracted from `EscalationJob`'s previously-inline "lowest-id Admin" lookup, now also excludes
Inactive/OnLeave admins (`Status == Active` filter) and accepts an `excludeUserId` (needed by the
status-PATCH path above, since the target user's own Status change isn't visible to a fresh DB query
until after `SaveChangesAsync`). `EscalationJob` (`NotifyHub.Infrastructure/Escalation/EscalationJob.cs`)
now calls this shared resolver instead of its own inline query — same behavior, no test changes needed.

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
| `taskAssignmentChanged` | `UsersController.cs` (auto-forward on status change, increment 2); `TasksController.cs` (manual forward, increment 4) | `{ taskId, assignedStaffId }` |

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
`DemoOutboundMessageSeedStep` (10 demo messages: 5 appointment-reminder + 3 medication + 2 prescription, `DemoOutboundMessageSeedStep.cs:32-49` — corrected from a stale "5" here) → `PerformanceSeedStep` (step 6, FR-010, 45,000 outbound + 5,000 inbound at the default `targetMessageCount=50,000`, `OutboundRatio=0.9`).

Deterministic seed-only baseline for `outbound_messages`: 45,000 (perf) + 10 (demo) = 45,010. Any count above that is expected, not a seeding bug: `ReminderScheduler.CreateDueRemindersAsync` (`NotifyHub.Infrastructure/Reminders/ReminderScheduler.cs:121-133`) keeps inserting new rows over wall-clock time for the 10 real `PatientAppointmentSeedStep` appointments as their 48h/2h reminder windows open (up to 2 per appointment) — distinguish via `TriggerReference` prefix: `perfseed:*` (45,000), `appointment:*:created`/`medication:*:seed`/`prescription:*:seed` (10), `appointment:*:reminder:*h:*` (live, growing).

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

**Pages** (`src/pages/`): `LoginPage.tsx` (auth entry), `InboxPage.tsx` (thread list + `ConversationPanel`), `TaskBoardPage.tsx` (status-filtered task list + `NewTaskForm`/`TaskDetailPanel`), `TemplatesPage.tsx` (§6b, step 6 — list + create form + inline per-row edit form via a shared `TemplateForm` component defined in the same file), `AuditLogPage.tsx` (§6b, step 6 — role-branches on `user.role`: Admin gets an actor filter + `/api/audit`, Staff gets `/api/audit/mine`; action/date-range filters, paginated table, empty state). Date range: `from`/`to` are `<input type="date">` (day-granularity), defaulting on mount to the last 7 days (`from` = today-7, `to` = today, via `defaultFrom`/`toDateInputValue`). Converted to instants for the query string as UTC midnight for `from` and `T23:59:59.999Z` for `to` (`AuditLogPage.tsx:28-32`) — `to` must mean end-of-day, not start-of-day, otherwise a same-day `from`==`to` range collapses to one instant and matches nothing against `AuditController.QueryAsync`'s `OccurredAt <= to.Value` (:53-54).

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

**Routing**: `main.tsx:17-21` mounts `BrowserRouter`. Table in `App.tsx:13-26`: `/login` public (:14); `/`→`/inbox` redirect (:17), `/inbox`/`/tasks`/`/templates`/`/audit` (:18-21, last two added step 6) all under `<ProtectedRoute>`+`<AppShell>` (:15-16); `*`→`/` (:24). Every route now renders through `VersionedRoute` (§6a) rather than the bare page component directly — not reflected in the line numbers above, which still describe the pre-redesign table shape.

---

## 6a. UI redesign (v2) — presentation-layer only, no backend/API changes

A parallel "redesign" presentation exists behind a runtime toggle, entirely additive — every
legacy file listed in §6 above is unmodified.

- **Version toggle**: `src/config/uiVersion.ts` (localStorage key `notifyhub:ui-version`, build-time
  default via `VITE_UI_VERSION`) + `src/context/UIVersionContext.tsx` (`useUIVersion()` →
  `{version, setVersion, toggleVersion}`). Toggle button lives in `AppShell.tsx` (existing "Legacy
  UI/New UI" button, unchanged).
- **Route swap**: `src/routes/VersionedRoute.tsx` renders `Legacy` or `Redesign` per route based on
  `version`; wired into every route in `App.tsx`. Redesign screens live in `src/pages/v2/*PageV2.tsx`
  — currently pass-through stubs re-exporting the legacy page, replaced one at a time as each screen
  ships (Step 4 of the redesign process).
- **CSS scope**: `UIVersionContext.tsx` toggles a `redesign` class on `document.documentElement`
  (mirrors how a dark-mode toggle would drive `.dark`). `src/index.css` defines `.redesign` and
  `.redesign.dark` blocks that override the same shadcn CSS-variable token names
  (`--background`/`--primary`/`--popover`/etc.) with a distinct palette (cool slate neutrals +
  indigo/violet accent) — legacy screens never get this class, so they keep the untouched default
  shadcn zinc/slate theme (`:root`/`.dark` blocks, unchanged). Because every shared primitive
  (`Button`/`Card`/`Input`/`Badge`) already consumes these token names, they reskin automatically
  under the redesign scope with no per-component changes. Also added: `--popover`/`--popover-foreground`
  tokens (missing from the original token set — needed by `dialog`/`popover`/`select`/`dropdown-menu`/
  `command`, all newly added below) to `:root`/`.dark` too, since they were absent for every scope, not
  redesign-specific.
- **shadcn primitives added** (`src/components/ui/*`, via `npx shadcn add`, all Radix-based):
  `dialog`, `dropdown-menu`, `sheet`, `command` (+ `cmdk` dep), `select`, `popover`, `table`,
  `skeleton`, `avatar`, `tooltip`, `tabs`, `separator`, `scroll-area`. Available to both legacy and
  redesign code paths, but legacy doesn't reference any of them.
- **Shared redesign primitives** (`src/components/v2/`, redesign-only — legacy never imports these):
  - `status-badge.tsx` — `StatusBadge` component + `StatusTone` (`neutral`/`progress`/`success`/
    `danger`/`info`/`muted`), icon+color+label pairing used everywhere the app shows a state.
  - `status-config.ts` — per-domain config maps driving `StatusBadge`: `DELIVERY_STATUS_CONFIG`
    (keyed on `ThreadMessageDto.status`), `TASK_STATUS_CONFIG`/`TASK_PRIORITY_CONFIG` (keyed on
    `TaskStatus`/`TaskPriority`), `AUDIT_ACTION_CONFIG` (keyed on the 7 literal `AuditLogDto.action`
    strings emitted by `AuditLogger.Add` call sites — `send`/`receipt`/`opt-out`/`assignment`/
    `escalation`/`blocked`/`superseded`), `TRIGGER_TYPE_CONFIG` (keyed on `TemplateTriggerType`).
  - `initials-avatar.tsx` — `InitialsAvatar`, deterministic per-name color (simple string hash into
    a fixed tone palette) + initials, no new dependency.
  - `empty-state.tsx` — generic `EmptyState` shell; callers supply their own title/description so
    each screen's existing invitation-style copy (§6c) carries over unchanged.
  - `skeletons.tsx` — `ListRowSkeleton`/`TableRowSkeleton`/`CardGridSkeleton`, built on the new
    `Skeleton` primitive, replace the plain "Loading..." text per screen.
  - `sparkline.tsx` — `Sparkline` (mini bar chart) + `DistributionBar` (segmented proportion bar),
    plain SVG/CSS, no charting library — for the audit-activity and task-status-distribution strips
    planned in Step 4. Callers pre-aggregate data client-side from what `useThreads`/`useTasks`/
    `useTemplates`/`useAuditLog` already fetch; these components do no fetching themselves.
- **Command palette** (`Cmd/Ctrl+K`, redesign-only) — `AppShell.tsx`: `paletteOpen` state, a
  `keydown` listener (added/removed via `useEffect`, only when `isRedesign`), and a header trigger
  button (`Search... ⌘K`) all gated on `version === "redesign"`; no separate `AppShellV2`. Renders
  `components/v2/command-palette.tsx`'s `CommandPalette`, controlled via `open`/`onOpenChange` props.
  - Reads `useThreads()`/`useTasks()`/`useTemplates()` (already-cached TanStack Query data, no new
    fetch) into three `CommandGroup`s (Threads/Tasks/Templates) plus a "Quick actions" group
    ("New task"/"New template"), filtered client-side by `cmdk`'s built-in matcher against each
    `CommandItem`'s `value`. Task rows cross-reference `threadNameById` (built from the threads
    list) so tasks are searchable/labeled by patient name, not just task id.
  - Selecting a thread/task/template result navigates to `/inbox?thread={id}`,
    `/tasks?task={id}`, or `/templates?template={id}` — these query params are inert today (the
    `*PageV2.tsx` stubs re-export the legacy pages, which don't read them); each redesigned screen
    is expected to read its param and deep-link to that item once built.
  - "New task" opens a `Dialog` wrapping the existing (untouched) `NewTaskForm`
    (`components/tasks/NewTaskForm.tsx`). "New template" opens a `Dialog` wrapping a new
    `components/v2/quick-create-template-form.tsx` (`QuickCreateTemplateForm`) — a standalone
    create-only form using `useCreateTemplateMutation` directly, written because legacy
    `TemplatesPage.tsx`'s `TemplateForm` is a private unexported function in that (untouched) file.
- **`LoginPageV2.tsx`** — built. Same validation (required username/password, inline field errors)
  and auth flow (`useAuth().login`, `navigate("/", { replace: true })` on success, `toast.error`
  on failure) as legacy `LoginPage.tsx` — presentation-only change. Centered card on a static
  radial-gradient canvas (`hsl(var(--primary) / 0.14)`, no animation), a generated bell mark (no
  logo asset exists), `Loader2` spinner in the submit button while `isLoggingIn`. Outside
  `AppShell`, so no command palette here, matching the redesign plan.
- **`InboxPageV2.tsx`** — built. Two-pane layout (`ThreadList` + `ConversationPanelV2`, both
  `components/v2/`), same hooks/mutations as legacy (`useThreads`, `useThread`,
  `useAssignMutation`, `useReplyMutation`, direct `apiClient` pagination for older messages) —
  presentation-only.
  - `ThreadList` — client-side search (substring over `patientName`/`assignedStaffUsername`),
    `InitialsAvatar` per row, unread-count badge, opted-out indicator. **Deliberately no
    last-message preview/timestamp**: `ThreadDto`/`GET /api/threads` carries no last-message
    field, and fetching each thread's detail just to populate a sidebar preview would cost one
    extra request per visible row (up to 100) — flagged as a real gap vs. the original plan, not
    silently faked.
  - `ConversationPanelV2` — same message-bubble/pagination/scroll logic as legacy
    `ConversationPanel.tsx`, plus the new piece: a `StatusBadge` (from `status-config.ts`'s
    `DELIVERY_STATUS_CONFIG`) rendered under each outbound bubble from `ThreadMessageDto.status`
    (previously fetched, never rendered). Reuses the untouched legacy `CreateTaskForm` as-is for
    "Make task" (same reuse pattern as the command palette's "New task").
  - `?thread={id}` query param (`useSearchParams`) drives selection both ways — the command
    palette's thread links now resolve to the actual conversation, not just the Inbox route.
- **`TaskBoardPageV2.tsx`** — built. Kanban board (`components/v2/task-card.tsx`'s `TaskCard`,
  one column per `TaskStatus`; Completed/Cancelled collapsed by default, toggleable) plus a
  `Board`/`List` tab toggle (list = same cards, flat, sorted by `dueAt`) — both views over the
  same `useTasks()` (all statuses, unfiltered — the board draws the status split client-side,
  unlike legacy's single-status server query) and `useThreads()` (for thread-name
  cross-referencing on cards, same pattern as the command palette).
  - Filters (priority `Select`, assignee `Select`, recurring-only toggle) are client-side over
    the fetched task list. Distribution strip (`DistributionBar`) above the board reflects the
    filtered set.
  - Clicking a card opens `components/v2/task-detail-sheet.tsx`'s `TaskDetailSheet` — same
    `useTask(taskId)` hook as legacy `TaskDetailPanel`, so opening it still fires BR-014's
    escalated→in-progress revert exactly as before; "Assign to me"/"Complete" call the same
    `useUpdateTaskMutation()` instance the board owns (one shared mutation, matching legacy's
    single-instance-for-all-rows behavior). `?task={id}` drives the sheet the same way Inbox's
    `?thread={id}` drives thread selection.
  - "New task" reuses the untouched legacy `NewTaskForm` in a `Dialog` (same reuse pattern as
    the command palette and Inbox's "Make task").
  - **Same thread-name cross-reference gap as the command palette**: cards/sheet fall back to
    `Thread #{id}` when the task's thread isn't in the first 100 threads `useThreads()` fetched
    (`GET /api/threads` sorts newest-first) — only matters for tasks tied to old/inactive
    threads, not a bug, just the documented degrade path.
- **`TemplatesPageV2.tsx`** — built. List + live-preview split pane (Postmark pattern):
  left = compact rows (name, `TRIGGER_TYPE_CONFIG` badge, offset-hours), right = selected
  template's rendered preview or its edit form. Same validation and mutations as legacy
  (`useCreateTemplateMutation`/`useUpdateTemplateMutation`), factored into a reusable
  `components/v2/template-form.tsx` (`TemplateForm`, presentation-only — legacy's inline
  `TemplateForm` in `TemplatesPage.tsx` isn't exported, so this is a new component, not a
  refactor of legacy). `?template={id}` deep-links like the other screens.
  - **Merge-field preview** (`components/v2/merge-field-text.tsx`, `MergeFieldText`) — the
    piece the audit flagged as entirely missing. Two modes: "Raw source" highlights every
    `{{field}}` token as a literal chip; "Sample preview" substitutes illustrative values for
    exactly the two fields `NotifyHub.Domain/Messaging/TemplateRenderer.cs` actually resolves
    at send time (`patient_name` always, `appointment_time` for `AppointmentReminder` sends) —
    any other field name stays shown as an unresolved token in both modes, mirroring
    `TemplateRenderer.Render`'s real "leave unknown fields as-is" behavior rather than
    fabricating values for fields the backend wouldn't actually fill in.
- **`AuditLogPageV2.tsx`** — built, all 5 redesign screens now complete. GitHub/Datadog-style
  table: sticky header, `TableRow` hover highlight, monospace actor/timestamp columns,
  client-side column sort (re-sorts the current page only — server pagination unchanged),
  `Action` column via `StatusBadge`/`AUDIT_ACTION_CONFIG` (icon+color per the 7 action types),
  a day-by-day `Sparkline` above the table (counts from the current page/filter only, not a
  true full-history aggregate — no new endpoint).
  - **Product decision, not a technical one**: the redesign restricts this screen to Admin
    only — legacy's Staff-scoped `/api/audit/mine` ("your own actions") view is intentionally
    dropped in the new UI. Enforced twice: `AppShell.tsx`'s `NAV_LINKS` gets a new
    `adminOnlyInRedesign` flag filtered out of `visibleNavLinks` when `isRedesign &&
    user?.role !== "Admin"` (nav link hidden), and `AuditLogPageV2` itself renders an
    "Admins only" `EmptyState` and skips calling `useAuditLog` entirely for non-Admins, so a
    direct `/audit` visit by Staff doesn't fall through to the `/api/audit/mine` view either.
    Legacy is completely unaffected (`AppShell.tsx`'s `NAV_LINKS` array itself, and legacy's
    `AuditLogPage.tsx`, are unchanged — only the redesign's render-time filter is new).

All 5 redesign screens + the command palette are now built. Nothing left unbuilt from the
original redesign plan.

---

## 7. Test structure

### Domain (`NotifyHub.Tests/NotifyHub.Domain.Tests/`) — no DB
Files: `PasswordPolicyTests.cs`, `TemplateRendererTests.cs`, `IdempotencyKeyGeneratorTests.cs`, `RetryBackoffPolicyTests.cs`, `OptOutKeywordMatcherTests.cs`, `TaskDueDateDefaultsTests.cs`, `RecurrenceCalculatorTests.cs`, `ReminderDueCalculatorTests.cs`, `ReminderTriggerReferenceTests.cs`.
Run: `dotnet test NotifyHub.Tests/NotifyHub.Domain.Tests`

### Integration (`NotifyHub.Tests/NotifyHub.Integration.Tests/`)
| Test file | Factory |
|---|---|
| `AuthEndpointTests.cs`, `EscalationJobTests.cs`, `InboundWebhookTests.cs`, `MessageDispatcherOptOutTests.cs`, `TasksControllerTests.cs`, `ThreadsControllerTests.cs`, `ReminderSchedulerTests.cs`, `AuditControllerTests.cs`, `TemplatesControllerTests.cs`, `UsersControllerTests.cs` (added increment 2 — create/assignable-filtering/auto-forward-on-status-change) | `CustomWebApplicationFactory` (EF Core InMemory) |
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
