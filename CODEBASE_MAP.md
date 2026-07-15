# NotifyHub — Codebase Map

Reality-first: what's actually implemented, with file:line citations. Not a restatement of
`PROJECT_CONTEXT.md`'s requirements — for intentions, deviations, and step-by-step history see
`PROJECT_CONTEXT.md` (spec) and `STATUS.md` (build log, deviations, known limitations). If this
file ever contradicts the code, the code wins — fix this file and flag the discrepancy.

Last verified against commit `1c6c47b` (step 6, committed and reviewed) plus step 7's
documentation additions (README, ADRs, security/AI-log docs, coverage report) and step 7's
fix rounds (CI dependency-vulnerability scan + vulnerable transitive package pins;
`WebhooksController.Inbound`'s `UnreadCount` increment made atomic), plus step 8's 14-increment
feature set (Task type/description/forwarding/active-flag/filters, Dashboard, favicon, UI
redesign lock-in, Template bookmarks, scheduled sends/Quiet Hours/rate limiting/new-patient SMS,
User Management with auto-forward, a 7-tab Settings module, realistic seed data, top-nav task
widget — see `STATUS.md`'s "Step 8 checklist" for the full breakdown) — all 14 increments
committed individually, each with its own migration/tests where applicable.

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

**Bug fix — UTC timestamp round-trip (model-wide)**: `NotifyHubDbContext.ConfigureConventions`
applies a `ValueConverter` to every `DateTime`/`DateTime?` property in the model, forcing
`DateTimeKind.Utc` on read. Pomelo/MySQL drops `DateTimeKind` on round-trip (values always come
back `Unspecified`), which made `System.Text.Json` omit the `Z` suffix on every timestamp field,
which in turn made the frontend's otherwise-correct `new Date(x).toLocaleString()` (e.g.
`ConversationPanel.tsx:175`, `TaskBoardPageV2.tsx:330`) misparse the UTC instant as local time —
showing message timestamps and task due dates a day off (direction/size depends on the browser's
UTC offset). Per §11a the DB value was always correct UTC; only the `DateTimeKind` tag on read
was lost. Fixed once at the DbContext level (not per-entity) since it affects every `DateTime`
column — `OutboundMessage.CreatedAt`, `InboundMessage.ReceivedAt`, `TaskItem.DueAt`/
`RecurrenceEndDate`, `AuditLog.OccurredAt`, etc. No data migration needed (conversion applies at
materialization) and no schema change (still a plain `datetime` column, confirmed via
`dotnet ef migrations add` producing an empty `Up`/`Down`).

| Entity → table | Entity file | Config file | Key fields | Relationships / indexes |
|---|---|---|---|---|
| `User` → `users` | `NotifyHub.Domain/Entities/User.cs:5` | `UserConfiguration.cs:7` | Id, Username, PasswordHash, Role, **FullName?, Status** (added, see below), **LeaveFrom?, LeaveTo?** (added P9-12, both required together when `Status` is set to `OnLeave`) | Unique index `Username` (:18) |
| `RefreshToken` → `refresh_tokens` | `RefreshToken.cs:3` | `RefreshTokenConfiguration.cs:7` | Id, UserId, TokenHash, ExpiresAt, RevokedAt | Unique index `TokenHash` (:18); index `UserId` (:25); FK on `User` side |
| `Patient` → `patients` | `Patient.cs:4` | `PatientConfiguration.cs:7` | Id, Name, Phone, OptOutAt | Unique index `Phone` (:22) |
| `Appointment` → `appointments` (stub) | `Appointment.cs:6` | `AppointmentConfiguration.cs:7` | Id, PatientId, ScheduledAt, Status | FK `Patient`, cascade delete (:22-25); index `PatientId` (:27) |
| `MessageTemplate` → `message_templates` | `MessageTemplate.cs:5` | `MessageTemplateConfiguration.cs:7` | Id, Name, Body (≤1000), TriggerType, OffsetHours, **IsActive** (added increment 8, default `true`) | No indexes |
| `Bookmark` → `bookmarks` | `Bookmark.cs:5` (added increment 8) | `BookmarkConfiguration.cs:7` | Id, Label (≤100), Description (≤300), InsertText (≤1000) | No indexes, no relations — flat admin-curated snippet library (§5), e.g. Label="Patient Name"/InsertText="{{patient_name}}", inserted into a `MessageTemplate.Body` from the template editor's dropdown |
| `OutboundMessage` → `outbound_messages` | `OutboundMessage.cs:5` | `OutboundMessageConfiguration.cs:7` | Id, PatientId, ThreadId?, TemplateId?, SenderType, TriggerReference?, RenderedBody?, Status, IdempotencyKey?, AttemptCount, NextRetryAt?, **ScheduledAt?** (added increment 10), **SentByUsername?** (added P9-06, ≤100 chars, denormalized), **ExpiresAt?, ExpiryReason?** (added P9-07), **EventTime?, ReminderOffsetMinutes?, ReminderExpiryOffsetMinutes?, SentAt?** (added P9-08), **PduCount?** (added P9-09, sourced from the gateway receipt, immutable once set) | Unique index `IdempotencyKey` (:32); FKs Patient/Template/Thread all `Restrict` (:36-49); composite index `(Status, NextRetryAt)` (:52); composite index `(ThreadId, CreatedAt)` (:53); composite index `(Status, ExpiresAt)` (P9-07, drives the dispatcher's expiry sweep) |
| `SystemSetting` → `system_settings` | `SystemSetting.cs:5` (added increment 10) | `SystemSettingConfiguration.cs:7` | Key (PK, ≤100), Value (≤200) | No indexes — generic admin-editable key-value store (Quiet Hours, per-patient rate limiting), wrapped by `SettingsService` (typed accessors, no raw string parsing at call sites) |
| `TaskForwardingRule` → `task_forwarding_rules` | `TaskForwardingRule.cs` (added P9-10) | `TaskForwardingRuleConfiguration.cs` | Id, UserId, TargetUserId, From? (DateTime), To? (DateTime), Reason? (≤300), CreatedAt | FKs `User`/`TargetUser` both `Restrict`, two separate `.WithMany()` relationships to `User` (same disambiguation-by-convention pattern as `TaskItem.AssignedStaff`/`OriginalOwner`); index on `UserId`. No DB-level overlap constraint — MySQL has no exclusion-constraint equivalent for date-range overlap, so rules 4/9 are enforced application-side (`TaskForwardingRulesController`) |
| `DeliveryStatusHistory` → `delivery_status_history` | `DeliveryStatusHistory.cs:5` | `DeliveryStatusHistoryConfiguration.cs:7` | Id, MessageId, Status, OccurredAt | FK `Message`, cascade delete (:22-25); index `MessageId` (:27) |
| `AuditLog` → `audit_log` | `AuditLog.cs:4` | `AuditLogConfiguration.cs:7` | Id, Actor, Action, EntityType, EntityId, OccurredAt, Detail? | Composite index `(EntityType, EntityId)` (:20); index `Actor` (:21); no FK (polymorphic ref) |
| `ConversationThread` → `threads` | `ConversationThread.cs:7` | `ConversationThreadConfiguration.cs:7` | Id, PatientId, AssignedStaffId?, UnreadCount | **Unique index `PatientId`** (:21 — the race-safety guarantee, see §5); FK `Patient` `Restrict` (:17-20); FK `AssignedStaff` `Restrict` (:23-26); index `AssignedStaffId` (:29) |
| `InboundMessage` → `inbound_messages` | `InboundMessage.cs:4` | `InboundMessageConfiguration.cs:7` | Id, ThreadId, Body (≤1000), ReceivedAt | FK `Thread`, cascade delete (:18-21); composite index `(ThreadId, ReceivedAt)` (:24) |
| `TaskItem` → `tasks` | `TaskItem.cs:7` | `TaskItemConfiguration.cs:7` | Id, ThreadId, Priority, DueAt, Status, AssignedStaffId?, OriginalOwnerId, IsRecurring, RecurrenceIntervalDays?, RecurrenceEndDate?, RecurrenceMaxOccurrences?, OccurrenceCount, **Description? (≤1000), TaskType, IsActive** (added — see below) | FK `Thread` cascade (:29-32); FK `AssignedStaff`/`OriginalOwner` `Restrict` (:34-42); composite index `(Status, DueAt)` (:45, drives escalation job); index `AssignedStaffId` (:46) |

**Schema addition (P9-06)**: `OutboundMessage.SentByUsername` (string?, ≤100 chars) —
denormalized snapshot of the staff username who sent a message (`SenderType.Staff` only,
set at creation time in `ThreadsController.Reply`/`CreateConversation`), same "plain string,
not a live FK lookup" convention as `AuditLog.Actor`. Null for `SenderType.System` sends;
the SMS History report's `SenderUsername` DTO field falls back to `"System"` via
`m.SentByUsername ?? "System"` (also used for the `username` filter, so filtering by
`username=System` correctly matches system-dispatched rows). Migration
`20260714105800_AddSentByUsername` — nullable column, no bad-default risk (unlike increment
1's `AddTaskAndUserFields` migration). Pre-existing rows (everything sent before this
migration) have `SentByUsername = null` and correctly display as `"System"` even if they
were actually staff-sent — a real, permanent gap for historical data, not a bug; there was
no prior column to backfill from.

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
AppointmentBooking/FollowUp/Finance/General/ClinicalReview/Administrative/Other), `MessageStatus` (`:3`, Queued/Sending/Sent/Delivered/Failed/Superseded/Expired/**Cancelled** — Superseded (6th, step 5, BR-010) is now vestigial, see §4b, nothing sets it anymore since `ReminderScheduler` was retired in P9-08; Expired (7th) added P9-07, set by `MessageDispatcher.ExpireOverdueMessagesAsync`; Cancelled (8th) added P9-08, set by `MessagesController.Cancel`; none of the three terminal values are ever picked up by `MessageDispatcher`'s `Status == Queued` query),
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

**Global read-only enforcement (§7, this feature set)**: `ActiveUserRequiredFilter`
(`NotifyHub.Api/Users/ActiveUserRequiredFilter.cs`), registered alongside `AuthorizeFilter` in the
same `MvcOptions.Filters` list — an `IAsyncActionFilter`, so it always runs *after* `AuthorizeFilter`
(an `IAuthorizationFilter`) regardless of registration order, i.e. only ever sees already-
authenticated requests. Skips safe HTTP methods (GET/HEAD/OPTIONS) and any `[AllowAnonymous]`
action (login/refresh/logout, webhooks, mock-gateway). For everything else, looks up the caller's
live `User.Status` from the DB (deliberately not the JWT's claims — the access token is valid up to
`Jwt:AccessTokenMinutes`, so a claims-based check would let a just-deactivated user keep mutating
for up to 30 min) and returns 403 if not `Active`. Assignment-target validation was also added at
this point: `ThreadsController.Assign` and `TasksController.Update`'s `AssignedStaffId` branch now
both reject (400) a target user whose `Status != Active`, so Inactive/OnLeave users can't be handed
new work through those paths either.

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
| POST `api/threads` | `CreateConversation` (added increment 10) | default authenticated | §6: staff-initiated conversation with a brand-new patient — body `{name, phone, message, scheduledAt?}`; creates `Patient`+`ConversationThread`+first `OutboundMessage` in one call; 409 on duplicate phone (pre-check + `catch (DbUpdateException)` fallback, same pattern as `WebhooksController.FindOrCreateThreadAsync`); 400 if `scheduledAt` isn't in the future |
| POST `api/threads/{id}/messages` | `Reply` :139 | default authenticated | BR-001b opt-out check (:145-148); broadcasts `outboundMessageSent` (:169); **increment 10**: accepts optional `scheduledAt` (400 if not future), enforces the per-patient rate limit via `RateLimitExceededAsync` (429 if exceeded, no-op when `RateLimit:Enabled=false`) |
| POST `api/threads/{id}/assign` | `Assign` :177 | default authenticated | self-assign OK; assigning others requires caller role Admin, else 403 (:190-191); broadcasts `threadAssigned` (:203) |
| POST `api/threads/{id}/tasks` | `CreateTask` :209 | default authenticated | accepts `Description`/`TaskType` (increment 5); `Description` auto-populates server-side from `LatestMessageBodyAsync` (compares each table's single most-recent row, no full-history load) when the client omits it. **P9-10**: assignee is now resolved via `FallbackUserResolver.ResolveNewTaskAssigneeAsync` instead of the natural `AssignedStaffId ?? callerId` directly — see that method's writeup above |
| GET `api/threads/{id}/templates/{templateId}/preview` | `PreviewTemplate` (P9-04) | default authenticated | resolves a template's `{{patient_name}}`/`{{appointment_time}}` merge fields against the thread's real patient (+ next real `Scheduled` `Appointment` for that patient if one exists, else a generated dummy time `now.Date.AddDays(3).AddHours(10)`) via the existing `NotifyHub.Domain.Messaging.TemplateRenderer.Render` — reused as-is, no Domain-layer changes (Domain has no EF/HTTP deps, so the DB-querying field resolution lives here, not in `TemplateRenderer`). Returns `{ renderedBody }`; frontend fills this into the composer's editable textarea, not a locked preview. BR-013 unaffected — dispatch-time rendering (`MessageDispatcher.RenderAsync`) is untouched and still snapshots `RenderedBody` at actual send time from whatever the staff member ends up sending. |

### `UsersController` — `NotifyHub.Api/Controllers/UsersController.cs` (`[Route("api/users")]`, added this feature set/increment 2)
| Verb + route | Auth | Notes |
|---|---|---|
| GET `api/users` | `[Authorize(Roles="Admin")]` | filters `role`/`status`, paginated |
| GET `api/users/assignable` | default authenticated | returns `Status == Active` users only — the source every assignee-picker in the app should use (replaces the frontend's earlier dedupe-from-already-fetched-lists workaround) |
| POST `api/users` | `[Authorize(Roles="Admin")]` | creates a user (`PasswordPolicy`/`IPasswordHasher<User>`, same as `UserSeedStep`); 409 on duplicate username |
| PATCH `api/users/{id}/status` | `[Authorize(Roles="Admin")]` | sets `User.Status`; transitioning **to** Inactive/OnLeave auto-forwards that user's non-terminal tasks (`Status` not in `{Completed,Cancelled}`) to a fallback Active Admin in the **same `SaveChangesAsync`**, audits each (`action:"forward"`, actor `"system"`), broadcasts `taskAssignmentChanged` per forwarded task. **P9-12**: transitioning to `OnLeave` now requires `LeaveFrom`+`LeaveTo` both provided (400 if either is missing, or if `LeaveFrom > LeaveTo`) — stored on the `User` row; not cleared when transitioning away from `OnLeave`, left as a historical record and simply overwritten the next time this user goes `OnLeave` again. |

`FallbackUserResolver.ResolveFallbackAdminIdAsync` (`NotifyHub.Infrastructure/Users/FallbackUserResolver.cs`)
— extracted from `EscalationJob`'s previously-inline "lowest-id Admin" lookup, now also excludes
Inactive/OnLeave admins (`Status == Active` filter) and accepts an `excludeUserId` (needed by the
status-PATCH path above, since the target user's own Status change isn't visible to a fresh DB query
until after `SaveChangesAsync`). `EscalationJob` (`NotifyHub.Infrastructure/Escalation/EscalationJob.cs`)
now calls this shared resolver instead of its own inline query — same behavior, no test changes needed.

**P9-10**: `FallbackUserResolver` gained `ResolveNewTaskAssigneeAsync(db, naturalAssigneeId, ct)` —
a *separate* method, not a modification of `ResolveFallbackAdminIdAsync` above, since that
method is also called by `EscalationJob` and the status-PATCH mass-reassignment, neither of
which becomes forwarding-rule-aware (rule 2 is explicit the deactivation mass-reassignment
stays unchanged; escalation isn't "new task creation" either). Not a centralized "Assignment
Engine" refactor, per the plan's own explicit scoping-down. Resolution: if the natural
assignee is Active, use them directly (no forwarding lookup at all). Otherwise look up a
currently-in-window `TaskForwardingRule` for that user (`(From==null||From<=now) &&
(To==null||To>=now)` — rules 4/8/9 guarantee at most one match); if found and its target is
itself Active, use the target (rule 3, one level only — rule 5, the target's own rules are
never followed); otherwise fall through to the unchanged `ResolveFallbackAdminIdAsync`
Admin fallback (rule 6 covers a target that's gone Inactive since the rule was created).
Called from `ThreadsController.CreateTask` only — `naturalAssigneeId` (the thread's
`AssignedStaffId ?? callerId`) becomes `TaskItem.OriginalOwnerId` unconditionally, while the
*resolved* id becomes `AssignedStaffId`; a "forward" audit entry (`actor:"system"`) is
written whenever they differ, same convention as the existing auto-forward-on-deactivation
entries. `TaskForwardingRulesController` (`api/task-forwarding-rules`, self-service, scoped
server-side to the caller's own `UserId` — see below) is where rules are actually
created/edited/deleted.

### `TaskForwardingRulesController` — `NotifyHub.Api/Controllers/TaskForwardingRulesController.cs` (`[Route("api/task-forwarding-rules")]`, added P9-10)
| Verb + route | Auth | Notes |
|---|---|---|
| GET `api/task-forwarding-rules` | default authenticated | Lists only the caller's own rules |
| POST `api/task-forwarding-rules` | default authenticated | Body `{targetUserId, from?, to?, reason?}`. 400 if `targetUserId == caller` (rule 7) or target isn't `Status == Active` (rule 3); 409 if the (From,To) window overlaps an existing rule for the caller (rules 4/9, checked in-app — MySQL has no exclusion-constraint equivalent for date-range overlap) |
| PATCH `api/task-forwarding-rules/{id}` | default authenticated | 404 unless the rule belongs to the caller (self-service only — Admins can't edit others' rules through this endpoint). Full-replace semantics (target/window/reason together), not a sparse PATCH — From/To are themselves nullable, and a sparse-PATCH "did the caller mean to clear this?" ambiguity wasn't worth the complexity for an infrequently-edited config object |
| DELETE `api/task-forwarding-rules/{id}` | default authenticated | 404 unless the rule belongs to the caller. Rule 15 (forward/audit *history* permanently retained) refers to the per-task `AuditLog` "forward" entries already written at resolution time, not this row — deleting a rule only affects *future* resolutions (rule 10) |

Self-service scoping (every action limited to the caller's own `UserId`, no Admin-manages-
others-rules capability) is an inference: rule 7's "a user cannot set themselves as their own
forwarding target" reads first-person, and Settings → Task tab (where this is configured)
isn't Admin-gated like User Management is — flagged as a reasonable reading, not an explicit
requirement either way.

### `TasksController` — `NotifyHub.Api/Controllers/TasksController.cs` (`[Route("api/tasks")]` :17)
| Verb + route | Method:line | Auth | Notes |
|---|---|---|---|
| GET `api/tasks` | `List` :19 | default authenticated | filters (added increment 5): `status`, `assignedStaffId`, `description` (substring), `patientName` (substring, joins `Thread.Patient.Name` — no `Include`, EF auto-joins for a `Where` predicate), `dueFrom`/`dueTo` (range on `DueAt`), `isActive` (**defaults to `true` when omitted** — matches the Task screen's own "Active selected by default" filter) |
| GET `api/tasks/{id}` | `Detail` :51 | default authenticated | BR-014 auto-revert if opened by assignee (:58-64) |
| PATCH `api/tasks/{id}` | `Update` :69 | default authenticated | BR-014 auto-revert on assignee action (:119-124); recurrence spawn via `SpawnNextOccurrenceIfDue` (:133-159, now also carries `TaskType` to the next occurrence — `Description` deliberately doesn't, it was tied to whatever prompted the completed occurrence); `AssignedStaffId` branch now rejects (400) a non-Active target (§7, increment 3) — but still never audited, a pre-existing gap increment 4's `Forward` action doesn't retroactively fix; now also applies `Description`/`TaskType`(validated)/`IsActive` (increment 5) |
| POST `api/tasks/{id}/forward` | `Forward` (added increment 4) | default authenticated | manual task forwarding (§1) — body `{targetUserId, note?}`; rejects (400) a non-Active target; always audits (`action:"forward"`, detail includes the note if given) unlike the plain `PATCH` reassignment path above; broadcasts `taskAssignmentChanged`; deliberately leaves workflow `Status` untouched (forwarding an Escalated task keeps it Escalated for the new assignee — BR-014's auto-revert is about the current assignee acting on their own task, not who forwarded it to them) |

### `TemplatesController` — `NotifyHub.Api/Controllers/TemplatesController.cs` (`[Route("api/templates")]` :14)
| Verb + route | Method:line | Auth | Notes |
|---|---|---|---|
| GET `api/templates` | `List` :17 | default authenticated | optional `isActive` filter (increment 8) — omit to see everything, unlike Tasks' "defaults to Active" |
| POST `api/templates` | `Create` :35 | default authenticated | new templates default `IsActive=true` |
| PATCH `api/templates/{id}` | `Update` :59-89 | default authenticated | now also applies `IsActive` (increment 8); **P9-05**: when `Body` changes, sweeps every `Queued` `OutboundMessage` with a matching `TemplateId` and nulls `RenderedBody` — dual-safety net #1 ("no wrong SMS could send" per Zohaib's explicit call). Net #2, `MessageDispatcher.DispatchOneAsync`, already unconditionally re-renders from the live template on every dispatch attempt for any `TemplateId`-linked message — verified (not assumed) by reading the code: every current production creation path (`ThreadsController.Reply`/`CreateConversation`/`CreateReminder`, plus the now-retired `ReminderScheduler` at the time this was written) leaves `RenderedBody` null, so net #2 alone already fully covers propagation today; net #1 is kept anyway per the explicit dual-safety request and would be the only net that mattered if a future creation path ever pre-rendered `RenderedBody`. Same InMemory-vs-real-provider branch as `WebhooksController.Inbound`'s `UnreadCount` fix (`ExecuteUpdateAsync`'s `SetProperty` isn't translatable by the InMemory provider used by the fast test suite). |

Applies only non-null fields from `UpdateTemplateRequest` (`NotifyHub.Api/Templates/Dtos/UpdateTemplateRequest.cs`) — added step 6 to close §6b's "create/edit" gap (only `GET`/`POST` existed before).

### `BookmarksController` — `NotifyHub.Api/Controllers/BookmarksController.cs` (`[Route("api/bookmarks")]`, added increment 8)
| Verb + route | Auth |
|---|---|
| GET `api/bookmarks` | default authenticated |
| POST `api/bookmarks` | `[Authorize(Roles="Admin")]` |
| PATCH `api/bookmarks/{id}` | `[Authorize(Roles="Admin")]` |
| DELETE `api/bookmarks/{id}` | `[Authorize(Roles="Admin")]` |

Simple CRUD, no pagination (small admin-curated list). `List`'s in-memory `.Select(ToDto)` (not
inside the `IQueryable`) is deliberate — EF Core can't reliably translate a call to a static C#
mapping method into SQL, unlike `TemplatesController.List`'s inline `new TemplateDto {...}`
projection, which stays directly in the `Select()`.

### `SettingsController` — `NotifyHub.Api/Controllers/SettingsController.cs` (`[Route("api/settings")]`, added increment 10)
| Verb + route | Auth | Notes |
|---|---|---|
| GET `api/settings` | default authenticated | Quiet Hours + rate-limit config, via `SettingsService` |
| PATCH `api/settings` | `[Authorize(Roles="Admin")]` | partial update; validates `HH:mm` time strings and positive counts before writing |
| GET `api/settings/system-info` | default authenticated | **not** `SystemSetting`-backed — live diagnostics: `db.Database.CanConnectAsync()`, dispatcher/escalation poll intervals (the latter read straight from `IConfiguration`, key `Escalation:PollIntervalSeconds`). No reminder poll interval anymore (P9-08 retired the poll-based reminder engine — `SystemInfoDto.ReminderPollIntervalSeconds` removed) |

### `DashboardController` — `NotifyHub.Api/Controllers/DashboardController.cs` (`[Route("api/dashboard")]`, added increment 12)
| Verb + route | Auth | Notes |
|---|---|---|
| GET `api/dashboard/summary` | default authenticated | post-login landing page summary — pure read-side aggregation, no new business logic. `MyTasks` (`TaskCountsDto`: Open/InProgress/Escalated/Overdue) always scoped to the caller; `OrgTasks` (same shape, org-wide) is `null` for non-Admins; `UnreadThreadCount` = count of threads with `UnreadCount > 0`; `RecentActivity` = last 10 `AuditLogDto` rows, scoped to the caller's own actions for Staff (mirrors `AuditController`'s Admin/Staff split) |

### `MessagesController` — `NotifyHub.Api/Controllers/MessagesController.cs` (`[Route("api/messages")]`, added P9-06)
| Verb + route | Auth | Notes |
|---|---|---|
| GET `api/messages` | `[Authorize(Roles="Admin")]` | SMS History report — filters `patientName`/`phone` (substring, via `Patient` nav-property join, no `Include` needed), `username` (substring against `SentByUsername ?? "System"`), `text` (substring against `RenderedBody`), `status` (`MessageStatus` enum), `from`/`to` (range on `CreatedAt`); paginated (`PagedResult<T>.Clamp`, same pattern as every other list endpoint). Returns `SmsHistoryPagedResult` — `TotalCount` doubles as the report's "Total SMS" summary figure (already filter-scoped). **All columns now fully wired**: `ScheduledTime` (`ScheduledAt`, since increment 10), `ExpiryTime` (`ExpiresAt`, since P9-07), `PduCount` (`PduCount`, since P9-09 — null/"pending" until a receipt lands). `TotalPduCount` = `query.SumAsync(m => (int?)m.PduCount ?? 0)` across the **whole filtered set**, not just the current page (a separate aggregate query from the paginated `items` query). |
| PATCH `api/messages/{id}` | default authenticated | `UpdateReminder` — see §4b |
| POST `api/messages/{id}/cancel` | default authenticated | `Cancel` — see §4b |

`SettingsService` (`NotifyHub.Infrastructure/Settings/SettingsService.cs`) — typed accessors
(`GetQuietHoursAsync`, `GetRateLimitAsync`, `IsQuietHoursNowAsync`) over the generic
`SystemSetting` key-value table; `SetAsync` upserts. Both Quiet Hours and rate limiting default
to **disabled** (seeded by `SystemSettingSeedStep`) so existing dispatch behavior is unchanged
until an Admin opts in via `PATCH api/settings`.

### `AuditController` — `NotifyHub.Api/Controllers/AuditController.cs` (`[Route("api/audit")]` :17-19, step 6/FR-011)
| Verb + route | Method:line | Auth | Notes |
|---|---|---|---|
| GET `api/audit` | `List` :21-27 | `[Authorize(Roles="Admin")]` (:22) — first non-default, non-webhook auth policy in the codebase | filters `actor`/`action`/`from`/`to`, paginated via `PagedResult<T>.Clamp` |
| GET `api/audit/mine` | `Mine` :29-35 | default authenticated | same filters minus `actor` — server hardcodes `actor` to the caller's own username (:33), ignoring any client value |

Shared query logic: `QueryAsync` (:37-63).

### `WebhooksController` — `NotifyHub.Api/Controllers/WebhooksController.cs` (`[Route("api/webhooks")]` :18, class-level `[AllowAnonymous][SharedSecret]` :19-20)
| Verb + route | Method:line | Notes |
|---|---|---|
| POST `api/webhooks/gateway-receipt` | `GatewayReceipt` :26 | shared-secret only; broadcasts `messageStatusUpdated` after the status write when `message.ThreadId` is set (P9-02) — see SignalR table below. **P9-09**: persists `request.PduCount` to `OutboundMessage.PduCount` once, from whichever receipt lands first (`if (message.PduCount is null && request.PduCount is not null)`) — immutable afterward regardless of Delivered/Failed outcome or further retry receipts, same audit-integrity principle as `RenderedBody`/BR-013. |
| POST `api/webhooks/inbound` | `Inbound` :81 | shared-secret only; broadcasts `inboundMessageReceived` (:108-114) |

Race-safe find-or-create: `FindOrCreateThreadAsync` (:119-141) — see §5.

### `MockGatewayController` — `NotifyHub.Api/Controllers/MockGatewayController.cs` (`[Route("api/mock-gateway")]` :19, class-level `[AllowAnonymous][SharedSecret]` :20-21)
| Verb + route | Method:line | Notes |
|---|---|---|
| POST `api/mock-gateway/send` | `Send` :28 | called by Worker dispatcher (service-to-service); posts back to `api/webhooks/gateway-receipt` via named HttpClient `"self"` (:57-63). **P9-09**: computes `PduSegmentCount` from `message.RenderedBody` via `PduSegmentCalculator.CalculateSegmentCount` and includes it as `pduCount` in the receipt POST — sourced from the "carrier" (this mock gateway stands in for one), never recomputed by NotifyHub's own dispatcher, mirroring how a real carrier API returns segment counts in its webhook. |

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
| `taskAssignmentChanged` | `UsersController.cs` (auto-forward on status change, increment 2); `TasksController.cs` (manual forward, increment 4, implemented) | `{ taskId, assignedStaffId }` |
| `messageStatusUpdated` | `WebhooksController.GatewayReceipt` (P9-02) | `{ threadId, messageId, status }` — `status` is `message.Status.ToString()` (`Delivered`/`Queued`/`Failed`, whichever `GatewayReceipt` just set). Root cause of the pre-P9-02 "double tick" bug: `GatewayReceipt` updated the DB with no broadcast at all, so a delivery-status change was only ever visible after some unrelated refetch. `MockGatewayController.Send`'s earlier `Sent` transition deliberately still doesn't broadcast (out of P9-02's stated scope), so the single-tick `Sent` state is rarely seen live, only on a page load that happens to land mid-transition. Frontend: `useInboxHub.ts` invalidates `["thread", threadId]` only (not `["threads"]` — a status change doesn't affect the thread-list summary fields). Verified live end-to-end this session (see STATUS.md) via a scripted SignalR client — confirmed `outboundMessageSent` then `messageStatusUpdated {..., status:"Delivered"}` both arrive for a real reply. |

---

## 4. Background jobs

| Job | File:line | Trigger | What it does |
|---|---|---|---|
| `DispatcherWorker` | `NotifyHub.Worker/DispatcherWorker.cs:8-35` | Fixed 5s poll loop (:10, hardcoded, not config-driven); 5s error-retry delay (:11) | Resolves `MessageDispatcher` per scope, calls `DispatchDueMessagesAsync` (:22) |
| `MessageDispatcher` | `NotifyHub.Infrastructure/Messaging/MessageDispatcher.cs` | Called by `DispatcherWorker` | **Increment 10**: `DispatchDueMessagesAsync` now starts with a single Quiet Hours gate (`SettingsService.IsQuietHoursNowAsync` — if true, returns 0 immediately, no per-message state change; due messages simply stay `Queued` and get picked up on the next non-quiet poll) and the due-query also requires `ScheduledAt == null \|\| ScheduledAt <= now`. Otherwise unchanged: batch of 10 `Queued` messages due now, ordered by `CreatedAt`. `DispatchOneAsync` (:37-90): opt-out short-circuit (:42-50), renders template if set **and `RenderedBody` is still null** (post-Step-9: gated so a committed Reminder SMS body — rule 31 reversal, see §4b — is never overwritten; previously rendered unconditionally whenever `TemplateId` was set), POSTs to mock gateway (:66-67), on failure increments attempt count and either terminalizes via `RetryBackoffPolicy.IsTerminal` (:77-81) or requeues with backoff (:83-86). `RenderAsync` (:92-113) parses `TriggerReference` for `{{appointment_time}}` (:101-109). Constructor now also takes `SettingsService` — any direct `new MessageDispatcher(...)` call site (tests) needs the 4th arg. **P9-07**: `DispatchDueMessagesAsync` now calls a new private `ExpireOverdueMessagesAsync` *before* the Quiet Hours gate (not after) — marks `Expired` any `Queued` message with a passed `ExpiresAt`, sets `ExpiryReason` (fact-based: "before any send attempt"/"after N send attempt(s)", not a guessed specific cause like "quiet hours" — no per-message signal exists to know that for certain), adds a `DeliveryStatusHistory` row, audits `action:"expired"`. Deliberately checked unconditionally regardless of Quiet Hours: a message can sit `Queued` through its whole 12h window while Quiet Hours suppresses the batch entirely, which is the realistic way expiry gets hit in practice (BR-011's own retry/backoff, max ~31 min across 6 attempts, almost never reaches 12h alone) — if expiry ran after the Quiet Hours early-return, it would never fire during that exact scenario. Verified live end-to-end against the real Docker/MySQL stack (worker stopped, a message created and its `ExpiresAt` backdated via direct SQL, worker restarted, confirmed `Expired` + history + audit all landed on the next 5s poll). |
| `EscalationWorker` | `NotifyHub.Worker/EscalationWorker.cs:8-36` | Config-driven poll, `Escalation:PollIntervalSeconds` default 60s (:17); 5s error-retry delay (:13) | Resolves `EscalationJob` per scope, calls `EscalateOverdueTasksAsync` (:26) |
| `EscalationJob` | `NotifyHub.Infrastructure/Escalation/EscalationJob.cs` | Called by `EscalationWorker` | `EscalateOverdueTasksAsync` (:19-61): batch of 100 overdue non-terminal tasks (:23-29), resolves lowest-id Admin as fallback (:36-40), sets `Escalated` + audits (:45-47), reassigns + audits "auto-reassigned" if not already assigned to that admin (:49-54). **P9-12**: `EscalationWorker` also calls a new `RevertExpiredLeaveAsync` every poll cycle (piggybacking on this existing periodic job/poll rather than a new worker process, per the plan) — finds every `Status == OnLeave` user whose `LeaveTo` has passed, flips them back to `Active`, audits (`action:"status-change"`, actor `"system"`, entityType `"User"`). Unrelated to task escalation, just co-located for the free poll loop. |
**`ReminderWorker`/`ReminderScheduler` retired in P9-08**, deleted entirely (not just
unregistered) — `NotifyHub.Worker/ReminderWorker.cs`, `NotifyHub.Infrastructure/Reminders/*`,
plus the Domain helpers only they used (`ReminderDueCalculator`, `ReminderTriggerReference`)
and their test files. The `MessageStatus.Superseded` value they used (BR-010, "appointment
rescheduled while a reminder was still queued") is now vestigial — still a valid historical
value on old rows, but nothing in the codebase sets it anymore since there's no more
Appointment-polling reminder flow to detect a stale queued reminder against. Replaced by the
generic Reminder SMS engine — see §4b.

---

## 4b. Reminder SMS engine (P9-08)

Event-based reminders, generic and deliberately independent of the `Appointment` entity
(rule 34 — reusable for any future event-based reminder: payments, document expiry,
renewals, follow-ups). No parallel send path (rule 22) — a Reminder SMS is just an
`OutboundMessage` row with a few extra fields, flowing through the exact same
`MessageDispatcher`/mock-gateway/retry/expiry pipeline as a Standard SMS.

**Schema** (`OutboundMessage`, all nullable — populated only for Reminder SMS):
`EventTime` (the caller-supplied instant, rule 3), `ReminderOffsetMinutes`/
`ReminderExpiryOffsetMinutes` (snapshotted from `SettingsService.GetReminderAsync` at
creation time — rule 7, a later Settings change never applies retroactively), `SentAt`
(rule 32 "Sent Time" — set by `MockGatewayController.Send` alongside its existing `Sent`
transition; applies to Standard SMS too, not Reminder-only, since rule 22 means both types
share the write path). `MessageStatus` gained an 8th value, `Cancelled` (rules 28/29,
terminal, never picked up again — same pattern as `Expired`/`Superseded`). Migration
`20260714113500_AddReminderSmsFields` (approximate name/timestamp — nullable columns, no
bad-default risk).

**Settings** — two new `SystemSetting` keys via `SettingsService.GetReminderAsync`/
`ReminderOffsetMinutesKey`/`ReminderExpiryOffsetMinutesKey`: default 1440 min (24h) / 15 min
(rules 6/16), seeded by `SystemSettingSeedStep`. Unlike Quiet Hours/rate limiting, there's no
"enabled" flag — Reminder SMS creation is an always-on capability, not a gate on existing
behavior. Exposed via `GET`/`PATCH api/settings` (`SettingsDto.reminderOffsetMinutes`/
`reminderExpiryOffsetMinutes`), Settings → SMS tab (`sms-tab.tsx`'s new "Reminder SMS
defaults" card).

**Pure calculations** (`NotifyHub.Domain/Messaging/ReminderScheduleCalculator.cs`):
`CalculateScheduledSendTime(eventTime, offsetMinutes)` = `eventTime - offset` (rule 5);
`CalculateExpiryTime(eventTime, expiryOffsetMinutes)` = `eventTime - expiryOffset` (rules
15/17/18 — never derived from Created/Scheduled Time, unlike `MessageExpiryCalculator`'s
Standard SMS math); `MinSelectableEventTime(now, offsetMinutes)` = `now + offset` (rule 9).
`IdempotencyKeyGenerator.GenerateForReminder(patientId, templateId, eventTime,
reminderOffsetMinutes)` — separate hash input from Standard SMS's `Generate` (rule 30), so
the two families of idempotency keys never collide.

### API — `ThreadsController` (`NotifyHub.Api/Controllers/ThreadsController.cs`)
| Verb + route | Notes |
|---|---|
| POST `api/threads/{id}/reminders` | `CreateReminder` — body `{templateId, eventTime, body?}`, no manual Scheduled Send Time field (rule 4). Loads thread/patient/template (404 if either missing), snapshots current `ReminderSettings`, computes `ScheduledSendTime`/`ExpiryTime`, 400 if the computed Scheduled Send Time is already in the past (rules 8/9/10 — the real server-side enforcement, not just a UI hint), 409 on a duplicate (patientId+templateId+eventTime+offset) via `IdempotencyKeyGenerator.GenerateForReminder` + the existing unique index on `IdempotencyKey` as the race backstop. **Rule 31 reversal (post-Step-9 fix)**: `TemplateId` stays linked (kept for the idempotency hash/reporting above), but `body` — the Reminder SMS dialog's now-freely-editable text — is committed as `RenderedBody` at creation when provided (`string.IsNullOrWhiteSpace(request.Body) ? null : request.Body`); omitting `body` preserves the original "`RenderedBody = null`, rendered fresh at dispatch from the live template" behavior for backward compatibility. `MessageDispatcher.DispatchOneAsync`'s auto-render is now gated on `RenderedBody is null`, so a committed body is never overwritten at dispatch — but P9-05's template-edit sweep (`TemplatesController.Update`) still applies unmodified: editing the linked template nulls `RenderedBody` for any matching `Queued` message including a committed reminder, forcing a fresh render next dispatch (deliberate, not scoped to exclude reminders). Audits `reminder-created`. |

### API — `MessagesController` (`NotifyHub.Api/Controllers/MessagesController.cs`)
Class-level auth is no longer `[Authorize(Roles="Admin")]` — only `List` (the P9-06 report)
carries that attribute now; the two P9-08 actions below are default-authenticated (Staff can
manage reminders they create from a thread, same as any other message action).
| Verb + route | Notes |
|---|---|
| PATCH `api/messages/{id}` | `UpdateReminder` — body `{eventTime}`. 400 if `EventTime` is null (not a Reminder SMS) or `Status != Queued` ("already been sent", rule 27). Recomputes Scheduled Send Time/Expiry Time from the message's **own stored** `ReminderOffsetMinutes`/`ReminderExpiryOffsetMinutes` (rule 7 — not current Settings), re-validates rule 8, recomputes and re-checks the idempotency key (409 on collision with a different existing row). Audits `reminder-updated`. |
| POST `api/messages/{id}/cancel` | `Cancel` — 400 if `EventTime` is null or `Status != Queued` (rules 28/29, Reminder-SMS-only, still-Queued-only). Sets `Status = Cancelled`, adds a `DeliveryStatusHistory` row, audits `reminder-cancelled`. |

### Frontend
- `components/v2/reminder-sms-dialog.tsx` (`ReminderSmsDialog`) — the "Reminder SMS" action,
  same discoverability tier as "Insert template" in `conversation-panel.tsx`'s composer
  toolbar (new button there, `AlarmClock` icon). Template `Select` + `DateTimePicker` (P9-03)
  for Event Time, `minDate` set to `now + reminderOffsetMinutes` (day-granularity only, per
  P9-03's documented simplification) plus an exact submit-time re-check before calling the
  API. No manual Scheduled Send Time field anywhere in the UI (rule 4) — a read-only
  computed line shows what it resolves to.
  **Post-Step-9 fix — rule 31 reversed**: the body is now a freely-editable `Textarea`
  (`ref`-tracked, `bodyRef`), not a locked read-only preview. `handleTemplateChange` fetches
  P9-04's `GET .../templates/{id}/preview` and replaces the box's contents with the resolved
  text (same behavior as the composer's `handleInsertTemplate`/`setDraft`) — editable
  afterward, never reset by further typing. `insertAtCursor` (mirrors `TemplateForm`'s
  `insertBookmark`) inserts text at the `Textarea`'s current caret position, replacing any
  active selection, then restores focus with the caret placed immediately after the inserted
  text. Event Time insertion is wired through `DateTimePicker`'s new `onCommit` prop (see
  below) rather than `onChange` directly — `handleEventTimeCommit` calls `insertAtCursor` with
  the picked value formatted via `toLocaleString()` once the user finishes picking (not on
  every intermediate tick). Submits `{templateId, eventTime, body}`; the previous submit-time
  validation (must pick a template/Event Time) gained a third check (`body` can't be blank).
- `components/v2/date-time-picker.tsx`'s `DateTimePicker` gained an optional
  `onCommit?: (value: string) => void` prop — fires once when the popover closes (Done /
  outside click / Escape) while a value is set, unlike `onChange`, which fires continuously
  during interaction (once per clock-drag tick, via `ClockFace`'s `applyFromPointer`). Purely
  additive: every other call site (`NewTaskForm`/`CreateTaskForm`/`new-conversation-dialog`/
  `conversation-panel`/`task-tab`/`user-management-tab`/filter bars) doesn't pass it and is
  unaffected.
- `hooks/useThreads.ts`'s `useCreateReminderMutation(threadId)` mutation payload gained an
  optional `body?: string` field → `POST /api/threads/{id}/reminders`.
- `status-config.ts`'s `AUDIT_ACTION_CONFIG` gained `expired`/`reminder-created`/
  `reminder-updated`/`reminder-cancelled` entries; both Audit Log pages' `ACTIONS` filter
  list extended to match.
- `system-tab.tsx` lost its "Reminder poll interval" row (no more poll interval to report —
  `SystemInfoDto.reminderPollIntervalSeconds` removed from both the API DTO and its TS type).

### Verified live end-to-end (real Docker/MySQL stack)
Created a reminder via `curl`, confirmed `ScheduledAt`/`ExpiresAt`/offsets computed and
stored correctly in MySQL; confirmed exact-duplicate rejection (409) when reusing the same
event time (first attempt with a freshly-generated timestamp each call correctly did *not*
collide — different millisecond timestamps are legitimately different reminders, not a bug);
updated the Event Time and confirmed recalculation; cancelled it and confirmed a second
cancel attempt correctly 400s; confirmed the full audit trail
(`reminder-created`→`reminder-updated`→`reminder-cancelled`); confirmed the worker's SQL
logs show the expiry-sweep and due-query both querying the new columns with no errors and
no more `ReminderWorker` poll logs at all.

---

## 4a. Seed steps (`NotifyHub.Infrastructure/Seed/`, run in DI-registration order by `DbSeedRunner`)

All registered as `IDbSeedStep` in `NotifyHub.Api/Program.cs` (:55-61) and run unconditionally at
Api startup (`Program.cs` :105-106), including in every integration test that boots the Api
pipeline — no environment gating. Order: `UserSeedStep` → `SecondStaffSeedStep` →
`PatientAppointmentSeedStep` (10 demo patients+appointments, real-sounding names balanced across
Pakistani English/Indian/Chinese/Japanese locales — 3/3/2/2, not real patient data per BR-006) →
`TemplateSeedStep` (4 templates) →
`BookmarkSeedStep` (increment 8 — 2 bookmarks: "Patient Name"/`{{patient_name}}`, "Appointment
Time"/`{{appointment_time}}`, matching exactly what `TemplateRenderer` resolves at send time) →
`SystemSettingSeedStep` (increment 10 — default rows for every known setting key, idempotent
per-key rather than "any setting exists" so a future new key isn't skipped on an already-seeded
install; both Quiet Hours and rate limiting default disabled) → `DemoOutboundMessageSeedStep` (10 demo messages: 5 appointment-reminder + 3 medication + 2 prescription, `DemoOutboundMessageSeedStep.cs:32-49` — corrected from a stale "5" here) → `PerformanceSeedStep` (step 6, FR-010, 45,000 outbound + 5,000 inbound at the default `targetMessageCount=50,000`, `OutboundRatio=0.9`).

Deterministic seed-only baseline for `outbound_messages`: 45,000 (perf) + 10 (demo) = 45,010, **plus any Reminder SMS created through the P9-08 UI/API and any live-verification rows from Step 9 sessions** (both grow the count going forward — neither is a seeding bug). Historical note: before P9-08 retired it, `ReminderScheduler.CreateDueRemindersAsync` used to keep inserting new rows over wall-clock time for the 10 real `PatientAppointmentSeedStep` appointments as their 48h/2h reminder windows opened; that growth path no longer exists. Distinguish by `TriggerReference` prefix for the still-relevant static baseline: `perfseed:*` (45,000), `appointment:*:created`/`medication:*:seed`/`prescription:*:seed` (10) — `appointment:*:reminder:*h:*` (the old poll-based reminder rows) stopped growing once P9-08 shipped; new Reminder SMS rows have `TriggerReference = null` and are identifiable instead by a non-null `EventTime`.

`PerformanceSeedStep` (`NotifyHub.Infrastructure/Seed/PerformanceSeedStep.cs:31-151`) — constructor
parameter `targetMessageCount` (default 50,000), read from config key `Seed:PerformanceMessageCount`
in `Program.cs`'s DI registration. `RunAsync` (:81-83): idempotency check via a patient-**phone**
marker prefix (`+1777`, independent of `DemoOutboundMessageSeedStep`'s own "any message exists"
check) — switched from a patient-name prefix once the synthetic-patient names became realistic
(see below) and stopped being a stable marker. Patient names for the up-to-1,000 synthetic patients
are generated by `GenerateName` (:72-79) from four locale-specific first/last-name pools (Pakistani
English/Indian/Chinese/Japanese, ~20 names each, :48-70) instead of `"PerfSeed Patient 00001"`..
placeholders — round-robins the locale by index so the mix stays balanced, never mixes first/last
names across locales. Thread count scales with message target (~50 messages/thread, clamped
10-1,000, :93), 90/10 outbound/inbound split (`OutboundRatio`, :41), all outbound messages get a
terminal status (Delivered/Failed, :106 — never `Queued`, so `DispatcherWorker` never picks any of
them up), batched inserts via `SeedOutboundMessagesAsync`/`SeedInboundMessagesAsync`/`FlushAsync`
(chunks of 2,000, `AutoDetectChangesEnabled=false` during the loop).

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
| Standard SMS expiry calculation (P9-07) | `NotifyHub.Domain/Messaging/MessageExpiryCalculator.cs` | `CalculateExpiresAt(createdAt, scheduledAt)` = `(scheduledAt ?? createdAt) + 12h` (`DefaultExpiry`). Reminder SMS gets its own `EventTime`-anchored calculation in P9-08, not this one. |
| Recurrence calculation (BR-007) | `NotifyHub.Domain/Tasks/RecurrenceCalculator.cs:6-31` | `NextOccurrence`: due-date-anchored (`previousDueAt + intervalDays`, no drift); `null` if past `recurrenceEndDate` or over `recurrenceMaxOccurrences` |
| Opt-out keyword matching (FR-006) | `NotifyHub.Domain/Inbox/OptOutKeywordMatcher.cs:6-12` | Case-insensitive **exact** (trimmed) match against STOP/UNSUBSCRIBE/CANCEL/END/QUIT — not substring |
| Task due-date defaults (FR-008) | `NotifyHub.Domain/Tasks/TaskDueDateDefaults.cs:6-16` | Urgent+4h / High+1d / Medium+3d / Low+7d from creation |
| BR-014 escalation auto-revert | `NotifyHub.Api/Controllers/TasksController.cs` — two call sites: `Detail` :58-64 (on open by assignee), `Update` :119-124 (on any action by assignee that doesn't itself set a new status) | Flips `Escalated` → `InProgress` |
| Race-safe thread creation | `NotifyHub.Api/Controllers/WebhooksController.cs:119-141` (`FindOrCreateThreadAsync`) | Optimistic insert, `catch (DbUpdateException)` on the unique index (`ConversationThreadConfiguration.cs:21`), detach + re-read the winner (:138-139). **Now covered by a real-MySQL test** — `NotifyHub.Tests/NotifyHub.Integration.Tests/InboundWebhookThreadRaceMySqlTests.cs` (see §7); EF Core InMemory (used by every other integration test) can't reproduce genuine connection-level locking, so this was previously untested at the actual race. |
| ~~Reminder due-window calculation (FR-009)~~ | deleted P9-08 | `NotifyHub.Domain/Messaging/ReminderDueCalculator.cs` (`IsDue`) no longer exists — was appointment-window polling logic for the retired `ReminderScheduler` |
| ~~Reminder trigger-reference build/parse (BR-009/BR-010)~~ | deleted P9-08 | `NotifyHub.Domain/Messaging/ReminderTriggerReference.cs` no longer exists — was `appointment:{id}:reminder:{offsetHours}h:{ticks}` encoding for the retired `ReminderScheduler`'s reschedule-supersede logic |
| ~~Reminder scheduling + reschedule-supersede (FR-009/BR-003/BR-010)~~ | retired P9-08 | Poll-based `ReminderScheduler` deleted entirely — see §4b for its generic event-based replacement |
| Reminder SMS scheduling/expiry calculation (P9-08) | `NotifyHub.Domain/Messaging/ReminderScheduleCalculator.cs` | `CalculateScheduledSendTime`/`CalculateExpiryTime`/`MinSelectableEventTime` — all anchored to a caller-supplied `EventTime`, generic/no Appointment coupling (rule 34); see §4b |
| SMS segment (PDU) count calculation (P9-09) | `NotifyHub.Domain/Messaging/PduSegmentCalculator.cs` | `CalculateSegmentCount(text)` — GSM-7 (basic + extension table, GSM 03.38) if every character fits, else UCS-2; single-segment limits 160/70 chars, multi-segment 153/67 chars/segment; `SegmentCount = 1` if `length <= singleSegmentLimit` else `ceil(length / multiSegmentLimit)`. Called by `MockGatewayController.Send` (the "carrier"), not the dispatcher — mirrors how a real carrier API computes and returns segment counts. |
| Task forwarding rule overlap check (P9-10, rules 4/9) | `NotifyHub.Domain/Tasks/TaskForwardingRuleOverlap.cs` | `RangesOverlap(aFrom, aTo, bFrom, bTo)` — null bounds treated as `DateTime.MinValue`/`MaxValue` (open-ended); inclusive at touching boundaries. Pure; `TaskForwardingRulesController` is what actually queries+calls it per-user. |
| Per-patient rate limiting (§6, increment 10) | `NotifyHub.Domain/Messaging/RateLimitChecker.cs` (`IsAllowed`) | Pure comparison (`recentMessageCount < maxMessagesPerWindow`); the recent-count query itself lives in `ThreadsController.RateLimitExceededAsync` (counts `OutboundMessages` for the patient created within `SettingsService`'s configured window) |
| Quiet Hours window (§6, increment 10) | `NotifyHub.Domain/Messaging/QuietHoursCalculator.cs` (`IsQuietNow`) | `TimeOnly` comparison against a start/end window; handles the same-day case (`start < end`) and the wraps-past-midnight case (`start >= end`, e.g. 21:00-08:00) identically; zero-width window (`start == end`) is defined as never-quiet |
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
- `components/layout/AppShell.tsx` — top nav; mounts the single shared `useInboxHub()` connection (:18). `NAV_LINKS` (:8-13) now includes Dashboard/Templates/Audit log alongside Inbox/Task board (Dashboard added increment 13, `end: true` so it only matches the exact `/` path). Header also renders `components/v2/task-nav-widget.tsx`'s `TaskNavWidget` (increment 13, next to the Settings icon) — see §6a.
- `components/inbox/ConversationPanel.tsx` — merged inbound/outbound view, reply, assign, auto-scroll-if-at-bottom. Messages are paginated server-side (step 6/FR-010): `useThread` only returns page 1 (most recent); local `olderMessages` state (:26) accumulates additional pages fetched directly via `apiClient` (bypassing TanStack Query's cache, since this is an append-only local scrollback) when the "Load earlier messages" button (:146-152, shown while `hasMoreOlder`) is clicked.
- `components/inbox/CreateTaskForm.tsx` — inline "make task" form; now also collects `TaskType` (Select) and `Description` (Textarea, optional — blank submits fall through to the server's auto-populate-from-last-message default, increment 7). **P9-11**: "Recurring" checkbox reveals Interval (days, required)/End date (optional, `DateTimePicker` `mode="date"`)/Max occurrences (optional) — creation-time only, no edit-after-creation path (matches how `SpawnNextOccurrenceIfDue` already works). Backend (`CreateTaskRequest`/`ThreadsController.CreateTask`) already accepted all four `IsRecurring`/`RecurrenceIntervalDays`/`RecurrenceEndDate`/`RecurrenceMaxOccurrences` fields before this increment — verified (not assumed) by reading `NotifyHub.Api/Tasks/Dtos/CreateTaskRequest.cs`, confirming the plan's own "backend engine already exists, this is frontend-only" framing — no backend changes needed.
- `components/tasks/NewTaskForm.tsx` — thread-picker + priority + due date, now also `TaskType`/`Description` (same optional-blank behavior as `CreateTaskForm`, increment 7). **P9-11**: same recurring-toggle UI as `CreateTaskForm.tsx` above.
- `components/tasks/TaskDetailPanel.tsx` — fetching via `useTask(id)` (:12-19) is what triggers BR-014's server-side revert. Legacy-only, unmodified — the redesign's equivalent is `task-detail-sheet.tsx` below.
- `components/PriorityBadge.tsx`, `components/TaskStatusBadge.tsx` — color+label badges.
- `components/ui/*` — shadcn primitives (generated).

**Hooks** (`src/hooks/`):
- `useThreads.ts`: `useThreads()` :7 (list), `useThread(id)` :14 (detail — `messages` is now `PagedResult<ThreadMessageDto>`, page 1 only, step 6/FR-010; invalidates `["threads"]` since opening resets unread), `useReplyMutation` :30, `useAssignMutation` :41, `useCreateTaskMutation` :53.
- `useTasks.ts`: `useTasks(statusOrFilters?)` — accepts either a bare status shorthand (legacy `TaskBoardPage`) or a full `TaskListFilters` object (`TaskBoardPageV2`'s filter bar, increment 7: description/patientName/dueFrom/dueTo/isActive/assignedStaffId), `useTask(id)` (triggers BR-014 revert), `useUpdateTaskMutation()`, `useForwardTaskMutation()` (increment 7, `POST /api/tasks/{id}/forward`).
- `useInboxHub.ts`: `useInboxHub()` :20 — owns SignalR connection lifecycle + query invalidation.
- `useAudit.ts` (step 6): `useAuditLog(isAdmin, filters)` — picks `/api/audit` vs `/api/audit/mine` based on `isAdmin`, builds the query string from `actor`/`action`/`from`/`to`/`page`/`pageSize`.
- `useTemplates.ts` (step 6): `useTemplates(isActive?)` (list, `isActive` filter added increment 9), `useCreateTemplateMutation()`, `useUpdateTemplateMutation()` (PATCH, invalidates `["templates"]`).
- `useBookmarks.ts` (increment 9): `useBookmarks()` (list, any authenticated user), `useCreateBookmarkMutation()`/`useUpdateBookmarkMutation()`/`useDeleteBookmarkMutation()` (Admin-only server-side, used by the Settings > Template tab, increment 11). `apiClient` gained a `.delete()` method to support this (`lib/apiClient.ts`).
- `useUsers.ts` (this feature set, increment 6): `useAssignableUsers()` → `GET /api/users/assignable`, the roster every assignee-picker should use now — `TaskBoardPageV2.tsx`'s assignee filter switched to this from the old "dedupe usernames off already-fetched tasks" workaround (which could never surface a user with zero assigned tasks). Also `useUsers(filters)` (Admin list, powers the Settings > User Management tab, increment 11), `useCreateUserMutation()`, `useUpdateUserStatusMutation()` (invalidates both `["users"]` and `["tasks"]` since a status change can silently auto-forward tasks).
- `useSettings.ts` (increment 11): `useSettings()`/`useUpdateSettingsMutation()` (`GET`/`PATCH /api/settings`), `useSystemInfo()` (`GET /api/settings/system-info`) — back the Settings > SMS and > System tabs.
- `useDashboard.ts` (increment 13): `useDashboardSummary()` → `GET /api/dashboard/summary`.

**Auth wiring**:
- `context/AuthContext.tsx` — silent refresh-on-mount effect :41-57 (posts `/api/auth/refresh` with `skipAuth`, httpOnly cookie sent automatically); listens for `"auth:logout"` window event :36-38.
- `lib/tokenStore.ts:14` — in-memory singleton (`let tokens`), read via `getAccessToken()` :26-28.
- `routes/ProtectedRoute.tsx:11-17` — renders `null` while bootstrapping, redirects to `/login` if unauthenticated.

**SignalR wiring**:
- `lib/signalr.ts:9-14` — `createInboxConnection()`: hub URL `${BASE_URL}/hubs/inbox` (:11), JWT via `accessTokenFactory` (:12), `.withAutomaticReconnect()` (:14).
- `hooks/useInboxHub.ts` — connection created/started :24/:41, stopped on unmount :46. Listeners: `inboundMessageReceived` :26, `threadAssigned` :31, `outboundMessageSent` :36 — all invalidate `["threads"]`/`["thread", threadId]`.

**API client**: `lib/apiClient.ts` — JWT attached :46-49; 401 handling :53-68 (de-dupes concurrent refreshes via shared `refreshPromise` :22/:54-58, retries once, else clears token store + dispatches `"auth:logout"` :65-67). Base URL derivation: `lib/apiBaseUrl.ts:9-10` (`${protocol}//${hostname}:5000`, `VITE_API_URL` override available).

**Routing**: `main.tsx:17-21` mounts `BrowserRouter`. Table in `App.tsx:13-26`: `/login` public (:14); `/inbox`/`/tasks`/`/templates`/`/audit` (:18-21, last two added step 6) all under `<ProtectedRoute>`+`<AppShell>` (:15-16); `*`→`/` (:24). Every route now renders through `VersionedRoute` (§6a) rather than the bare page component directly — not reflected in the line numbers above, which still describe the pre-redesign table shape. **Increment 13**: `/` no longer redirects to `/inbox` — it renders the new unversioned `DashboardPage` (`src/pages/DashboardPage.tsx`) directly, same "no legacy variant needed" precedent as `SettingsPage`.

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
    `?thread={id}` drives thread selection. **Increment 7 additions**: `TaskType`/`Description`
    displayed (badge + text block if present); "Mark inactive"/"Mark active" toggle
    (`useUpdateTaskMutation` with `isActive`); "Forward" button opens a `Dialog` with a
    `useAssignableUsers()`-backed `Select` + optional note, calling the new
    `useForwardTaskMutation()` (`POST /api/tasks/{id}/forward`).
  - **Filter bar** (increment 7): Description/Patient (server-side substring filters),
    Due from/Due to (server-side range, defaults to today-6/today via the new shared
    `src/lib/dateRangeFilter.ts` util — `defaultFromDaysAgo(6)`/`toDateInputValue`/
    `toInstantRange`, extracted from what was previously duplicated verbatim in
    `AuditLogPage.tsx`/`AuditLogPageV2.tsx`, both now consume the same util — **bug fix**:
    all three functions were anchored to UTC (`toISOString()`/`setUTCDate`/explicit `Z`
    boundaries) instead of the viewer's local day, so the default "last N days" range and
    the actual query window sent to the server could be off by a day from what the date
    picker showed; now built from local `Date` getters/constructor args throughout, same
    convention as `date-time-picker.tsx`'s own `parseValue`/`formatDatePart`), Active/Inactive
    (server-side, defaults `"Active"`), Status (client-side, alongside the pre-existing
    Priority/Assignee/Recurring-only filters — kept client-side since the Board view needs
    every status at once to populate its columns). Assignee filter now sourced from
    `useAssignableUsers()` (increment 6) instead of the old dedupe-from-tasks hack.
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
  - **Increment 9 additions**: an Active/Inactive/All filter `Select` above the list (drives
    `useTemplates(isActive)`), an "Inactive" badge on list rows and the preview header for
    templates with `IsActive=false`. `TemplateForm` gained an "Insert bookmark" dropdown
    (`useBookmarks()`, inserts a bookmark's `InsertText` at the textarea's cursor position
    via a ref) and an `Active` checkbox (`TemplateFormValues.isActive`, default `true`);
    since `POST /api/templates` doesn't accept `IsActive`, creating a template as inactive is
    a create-then-PATCH two-step in `TemplatesPageV2.handleCreate`.
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
  true full-history aggregate — no new endpoint). **Bug fix**: the day-bucketing key was
  `log.occurredAt.slice(0, 10)` (the raw UTC calendar date), which misfiled entries near the
  viewer's local midnight into the wrong bucket; now built via `toDateInputValue(new
  Date(log.occurredAt))` (the same shared local-date helper as the filter bar, see below) so
  buckets reflect the viewer's local day.
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

- **Inbox "New conversation" flow** (§6, increment 11) — `components/v2/thread-list.tsx` gained a
  "New conversation" button above the search box, opening `components/v2/new-conversation-dialog.tsx`'s
  `NewConversationDialog` (name/phone/message + optional `datetime-local` schedule field, calling the
  new `useCreateConversationMutation()` in `hooks/useThreads.ts` → `POST /api/threads`). On success the
  new thread is selected via the same `onSelect`/`?thread={id}` mechanism `ThreadList` already used.
- **Settings module rebuilt** (§8, increment 11) — `pages/SettingsPage.tsx` (still unversioned, shared
  by both UI modes per §6) now renders 7 tabs (shadcn `Tabs`) instead of just the legacy/redesign
  toggle it held before: General (thin, read-only — `components/settings/general-tab.tsx`), SMS
  (Quiet Hours + rate-limit forms, **plus a P9-08 "Reminder SMS defaults" card** — `sms-tab.tsx`, backed by `useSettings.ts`), Task (read-only
  `TaskDueDateDefaults` display, **plus a P9-10 "Task forwarding" card (create/list/delete
  self-service `TaskForwardingRule`s, target picker via `useAssignableUsers` filtered to
  exclude the caller)** — `task-tab.tsx`, backed by `useTaskForwardingRules.ts`. **Bug fix**:
  the From/To date picker values (`mode="date"`, local "yyyy-MM-dd") were submitted via
  `new Date(value).toISOString()`, which JS parses as UTC midnight; displaying the
  round-tripped value back with `toLocaleDateString()` then converted through the *local*
  offset, shifting the shown date by a day for non-UTC viewers. Fixed by anchoring to local
  midnight on submit instead (`toLocalMidnightIso`, parses y/m/d and uses the multi-arg
  `Date` constructor — same convention as `date-time-picker.tsx`'s `parseValue`), so
  write/read now agree on the same calendar day; display unchanged), Template (Bookmark CRUD table —
  `template-tab.tsx`, backed by `useBookmarks.ts`), Notification (thin, client-only browser
  notification-permission toggle — `notification-tab.tsx`, no backend), User Management (Admin-only
  tab+content, user table + status `Select` per row + create-user form — `user-management-tab.tsx`,
  backed by `useUsers.ts`. **P9-12**: picking `OnLeave` in the status `Select` doesn't submit
  immediately — opens a `Dialog` collecting `LeaveFrom`/`LeaveTo` (both required, two
  `DateTimePicker`s) first, since a bare status change to `OnLeave` isn't itself enough
  information for the server to accept; the table also shows the leave window under the
  Select for any currently-`OnLeave` row), System (read-only diagnostics — `system-tab.tsx`, backed by
  `useSystemInfo()`). **The legacy/redesign toggle UI is gone** — dropped as part of this rebuild
  rather than deferred to a later polish pass, since General is meant to be thin/read-only and the
  toggle didn't fit any of the 7 requested tabs; `UIVersionContext`'s default (`"redesign"`) and
  `VersionedRoute`/legacy page files are all still intentionally untouched (§6a's redesign-lock
  decision), only the *manual switch UI* is gone. `AppShell.tsx`'s gear icon still links to
  `/settings` unchanged (swapping it for a text "Settings" nav link is a later polish item).
  Verified end-to-end against the live stack: all 7 tabs render, SMS tab loads real defaults from
  `GET /api/settings`, User Management lists the 3 seeded users with working status dropdowns, and
  the new-conversation dialog creates a patient+thread+message that immediately appears selected
  in the Inbox with a `Queued` status badge.
- **`DashboardPage` + top-nav task widget** (§10, increment 13) — `src/pages/DashboardPage.tsx`
  (unversioned, no legacy equivalent — entirely new screen). Stat cards (my open/escalated/
  overdue tasks, unread-thread count) + an Admin-only org-wide task-counts card + quick links to
  Inbox/Task board + a recent-activity list (reuses `AUDIT_ACTION_CONFIG`/`StatusBadge` styling
  from the Audit Log screen), all sourced from the one `useDashboardSummary()` call — no
  screen-specific aggregation logic on the frontend. `components/v2/task-nav-widget.tsx`'s
  `TaskNavWidget` (mounted in `AppShell.tsx`'s header): a `Popover` trigger showing a badge count
  of the caller's non-terminal (Open/InProgress/Escalated) assigned tasks (`useTasks({
  assignedStaffId, isActive: true })`, filtered client-side to those three statuses since
  `useTasks` only supports a single server-side `status` value, not a set), popover body lists up
  to 8; selecting one navigates to `/tasks?task={id}` — reuses `TaskBoardPageV2`'s existing
  deep-link mechanism to open `TaskDetailSheet` rather than duplicating a second modal renderer.
  Verified end-to-end: landing on `/` after login shows real stat-card numbers and recent-activity
  rows pulled from the live seed data, and the nav widget's badge count and popover list both
  match the caller's actual assigned tasks.

---

## 6e. Responsive design (Step 9 / P9-00)

Reflowed the v2 screens with Tailwind breakpoints (`sm`/`md`/`lg`, no new breakpoint system) —
same components at every width, nothing dropped, only rearranged:
- `components/layout/AppShell.tsx` — nav links collapse into a hamburger (`Menu` icon,
  `md:hidden`) opening a `Sheet` drawer (nav links + user identity) below `md`; the
  command-palette trigger becomes icon-only on `<sm`; username hides below `md`.
- `pages/v2/InboxPageV2.tsx` — `ThreadList`/`ConversationPanelV2` go single-pane below `md`
  (list hidden once a thread is selected, panel hidden until one is) instead of squeezing
  both side-by-side; `ConversationPanelV2` gained an `onBack?` prop rendering a `md:hidden`
  back arrow that clears the selection. Same pattern applied to `pages/v2/TemplatesPageV2.tsx`'s
  list/detail split pane (`handleBack` clears `?template=` and the `selected` state).
- `pages/v2/AuditLogPageV2.tsx` — the `Table` is `hidden md:block`; a `md:hidden` stacked
  card-list (same `sortedLogs` data, one card per row, every column still shown) replaces it
  below `md`, instead of horizontal-scrolling the table.
- `pages/v2/TaskBoardPageV2.tsx` — kanban board already horizontal-scrolled
  (`overflow-x-auto`) and had a List-tab fallback pre-existing; no structural change needed,
  satisfies the "every column reachable, not hidden" requirement as-is.
- `components/v2/task-detail-sheet.tsx` — the two paired action-button rows in the footer
  stack vertically below `sm` (`flex-col sm:flex-row`) instead of cramming two full-width
  buttons into a 75vw-wide `Sheet` on narrow screens.
- `pages/DashboardPage.tsx` — stat-card grids (`StatCard` row, org-wide task-counts row) are
  `grid-cols-1 sm:grid-cols-4` (full stack on mobile, not a 2-column grid); recent-activity
  rows wrap (`flex-wrap`) instead of clipping long actor/entity text against the timestamp.
- `pages/SettingsPage.tsx` — `TabsList` (7 tabs) gained `h-auto` alongside its existing
  `flex-wrap`; the primitive's default `h-10` was clipping a second wrapped row of tabs at
  narrow widths (pre-existing bug independent of P9-00, fixed while in this file).

Verification: `tsc -b`/`vite build` both clean; `docker compose up -d --build web` rebuilt
and reserved (Dockerfile bakes source at build time, no dev-server volume mount, so a
container rebuild is required to pick up any frontend change — not just a restart). No
browser/screenshot tool was available this session (unlike step 8's live click-through
verification, noted in STATUS.md) — verified via type-check/build plus reading the rendered
Tailwind classes, not a real-viewport visual pass; flagged as a real gap, not silently
claimed as done. The Playwright e2e suite was run as a functional smoke check but every spec
failed on `loginViaUi`'s `page.waitForURL("**/inbox")` — pre-existing staleness from
increment 13 (`LoginPage.tsx`/`LoginPageV2.tsx` both `navigate("/")`, and `/` has rendered
`DashboardPage` instead of redirecting to `/inbox` since increment 13), not a P9-00
regression. Out of scope to fix here (not part of `STEP9_PLAN.md`); flagged in STATUS.md.

---

## 6f. Step 9 quick fixes (P9-01)

- **Command palette removed entirely** (P9-01a) — `components/v2/command-palette.tsx` and its
  create-only `components/v2/quick-create-template-form.tsx` dependency deleted; `AppShell.tsx`
  lost `paletteOpen` state, the `Cmd/Ctrl+K` keydown listener, and both header trigger buttons
  (desktop text button + mobile icon button). The generated shadcn `command`/`cmdk` primitive
  (`components/ui/command.tsx`) is left in place (unused, not explicitly listed for removal —
  same category as other generated-but-idle primitives).
- **Task Forward dialog excludes the current assignee** (P9-01b) —
  `task-detail-sheet.tsx`'s Forward `Select` now filters `assignableUsers` by
  `u.id !== task?.assignedStaffId` client-side.
- **`TaskDetailSheet` auto-closes after any action taken from it** (P9-01c) — `handleToggleActive`/
  `handleForward` call `onOpenChange(false)` on success only (a failed action leaves the sheet
  open with its error toast visible); the new `handleAssignToMe`/`handleCompleteTask` wrappers
  call the parent-supplied `onAssignToMe`/`onComplete` then close unconditionally (those props
  are void-typed fire-and-forget from the sheet's perspective — the parent already toasts
  success/failure internally, so the sheet can't gate on the outcome without a prop-signature
  change, which wasn't in P9-01c's listed files).
- **Task creation date/time split** (P9-01d) — `NewTaskForm.tsx`/`CreateTaskForm.tsx`'s single
  `datetime-local` input became two: `type="date"` (`required`) + `type="time"` (optional,
  defaults `00:00` when blank via `` `${dueDate}T${dueTime || "00:00"}` ``). Submitting without a
  date now toasts an error client-side instead of falling through to the server's
  priority-based `TaskDueDateDefaults` default — the due date is no longer optional from this
  form. (P9-03 will later swap these native inputs for the shared `DateTimePicker`.)
- **`OffsetHours` removed from the Templates UI** (P9-01e) — `TemplateFormValues` no longer has
  an `offsetHours` field; `template-form.tsx` dropped the input and its validation.
  `TemplatesPageV2.handleCreate` sends a fixed `LEGACY_OFFSET_HOURS_PLACEHOLDER = 24` since the
  backend's `CreateTemplateRequest.OffsetHours` is still a required `[Range(1, int.MaxValue)]`
  int (schema/API untouched, per the plan, to avoid a breaking migration);
  `handleUpdate`/`PATCH` simply omits the field now (already nullable server-side, so omitting
  it leaves the stored value untouched). Both list-row and detail-header "offset Xh" displays
  removed. Backend `MessageTemplate.OffsetHours` column is unchanged and still returned by the
  API — just write-only-by-nobody until P9-08's Reminder SMS engine replaces its role for
  `AppointmentReminder` templates. Legacy `TemplatesPage.tsx` untouched (unreachable dead code
  per §6a).
- **Flagged, not silently resolved**: `STEP9_PLAN.md`'s own "Superseded/reversed decisions"
  section attributes the `ReminderOffset`/`ReminderExpiryOffset` Settings → SMS fields to
  "P9-10", but P9-10 in the same file is Task forwarding rules — P9-08 (Reminder SMS engine)
  rules 6/16 are what actually define those two settings. Treated as a typo for P9-08 (the only
  section that defines `ReminderOffset`/`ReminderExpiryOffset`), not silently corrected in the
  plan file itself.

---

## 6g. Shared `DateTimePicker` (P9-03)

- **New shadcn primitive**: `components/ui/calendar.tsx` (`npx shadcn add calendar`, react-day-picker
  v10 — a major-version jump from the v8-era templates shadcn usually ships, but the generated
  component matches the installed version and compiles clean). Note for future `shadcn add` runs
  in this repo: the CLI resolved the `@/` alias to a **literal `@` directory at the repo root**
  instead of `src/` (wrote `@/components/ui/calendar.tsx` and a duplicate default-template
  `@/components/ui/button.tsx`) rather than respecting `components.json`'s aliases — files were
  moved into place by hand and the stray `@/` directory deleted; the duplicate `button.tsx` was
  discarded (diffed against the real one first — different default shadcn template, never touched
  the project's actual customized button).
- **`components/v2/date-time-picker.tsx`** — `DateTimePicker`, a drop-in replacement for native
  `<input type="date">`/`<input type="datetime-local">`: same controlled `value`/`onChange` string
  shapes (`"yyyy-MM-dd"` / `"yyyy-MM-ddTHH:mm"`, local time), so callers that already do
  `new Date(value).toISOString()` didn't need that part rewritten. Props: `mode` (`"date"` |
  `"datetime"`, default `"datetime"`), `timeRequired` (default `true` — when `false`, picking a
  date immediately fills the time part with `00:00` until the user changes it, rather than leaving
  the value date-only), `minDate` (disables earlier calendar days — added now, unused until P9-08
  needs a min-selectable-`Event Time` per its rule 9/10; not wired to time-of-day granularity yet,
  flagged as a simplification to revisit if P9-08 needs exact-minute enforcement rather than
  date-level).
- **UI**: Popover-triggered. "Material-style date card" = the `Calendar` primitive with a
  primary-colored header banner (date/time step tabs) above it when `mode="datetime"`.
  "Clock-face time picker" = a custom `ClockFace` sub-component — a circular dial, pointer
  down/move/up over the whole disc computes an angle-from-center (`atan2`) and maps it to the
  nearest hour (12 positions) or minute (any of 60, continuous — not snapped to 5), with a
  Material-style hand+thumb rendered via `transform: rotate(...)` and percentage-based
  `left`/`top`. Hour selection auto-advances to minute selection; minute release closes the
  popover. AM/PM toggle buttons; clicking the digital `HH`/`MM` readout jumps directly to that
  phase.
- **Replaces native inputs at** (all 7 files from the plan): `NewTaskForm.tsx`/`CreateTaskForm.tsx`
  (`dueAt`, `timeRequired={false}` — collapses P9-01d's separate `dueDate`+`dueTime` state back
  into one field now that the shared picker handles the optional-time case itself),
  `new-conversation-dialog.tsx` (`scheduledAt`, optional), `conversation-panel.tsx`'s Schedule
  toggle (`scheduledAt`, optional, compact `h-7` className override), `TaskBoardPageV2.tsx`'s
  Due from/to filters (`mode="date"`), `AuditLogPageV2.tsx` and legacy `AuditLogPage.tsx`'s From/To
  filters (`mode="date"` — legacy touched too since P9-03 named both files explicitly, even though
  legacy is otherwise unreachable dead code per §6a/P9-00's own scope note).
- **Not yet verified live in a browser** (same caveat as P9-00 — no browser/screenshot tool
  available this session): `tsc -b`/`vite build` clean and `docker compose up -d --build web`
  confirmed serving, but the clock face's pointer-event angle math and the Popover step-switching
  were verified by code reading, not by actually dragging a clock hand in a real viewport. Flagged
  as the highest-risk unverified piece in this increment — recommend an actual click-through pass
  before treating P9-03 as fully done.

---

## 6h. SMS History report (P9-06)

- **`pages/SmsHistoryPage.tsx`** — unversioned, no legacy variant (entirely new screen,
  same precedent as `DashboardPage`/`SettingsPage`). Admin-only, guarded client-side
  (`EmptyState` "Admins only" for non-Admins, same pattern as `AuditLogPageV2`) in addition
  to the server's `[Authorize(Roles="Admin")]`. Filters (patient/sender/phone/text/status/
  date range) + pagination + a two-tile summary row (Total SMS / Total PDU, both sourced
  directly from the response — no client-side aggregation). Responsive per P9-00's pattern:
  `Table` on `md+`, stacked cards below. `Expiry`/`PDU` columns render `"—"` until P9-07/P9-09
  populate the underlying data.
- **`hooks/useMessages.ts`** — `useSmsHistory(filters)` → `GET /api/messages`, same
  `buildQuery`/`useQuery` shape as `useAudit.ts`.
- **`types/messages.ts`** — `SmsHistoryDto`/`SmsHistoryPagedResult`; `MessageStatus` type
  already includes `"Expired"` even though the backend enum doesn't have that value until
  P9-07 (harmless to predeclare, avoids touching this file again for that increment).
- **Route** `/sms-history` in `App.tsx`, unversioned like `/settings`/`/`.
- **Nav link**: `AppShell.tsx`'s `NAV_LINKS` gained a new `adminOnly` flag (distinct from
  Audit log's existing `adminOnlyInRedesign` — SMS History is Admin-only in **every** UI
  mode since the server has no Staff-scoped variant at all, unlike Audit log's `/api/audit/mine`).
- Verified live against the real Docker/MySQL stack: staff gets 403, Admin gets 200 with
  real data; sent a real reply as staff and confirmed it shows up with `senderUsername:
  "staff"` (not "System") and that the `username=staff` filter matches it; pre-existing
  rows from before this migration correctly show `"System"` (no prior column to backfill
  sender identity from — a permanent gap for historical data, not a bug).

---

## 6i. Filter bar restyle — dense inline-label grid (this session)

Layout/styling-only change (no filter state, query logic, or API params touched) across
the three redesign screens with filter bars: `TaskBoardPageV2.tsx`, `SmsHistoryPage.tsx`
(P9-06), `AuditLogPageV2.tsx`. Legacy `AuditLogPage.tsx`/`TaskBoardPage.tsx` and SMS
History's Expiry/PDU columns were explicitly out of scope for this pass (Expiry-range/PDU-
range filters don't exist yet in `useMessages.ts`/`MessagesController` — display-only
columns today, so they weren't added as filter fields here; would need real filter params
added to `useSmsHistory`/`GET api/messages` first, flagged as a gap, not fixed).

- **`components/v2/filter-bar.tsx`** (new) — `FilterBar` (grid wrapper,
  `grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-x-6 gap-y-3`) + `FilterField` (one
  label+control row: `w-[100px] shrink-0` label with trailing colon, control area
  `min-w-0 flex-1`). Factored out once, used by all three screens instead of each
  duplicating its own `space-y-1.5` label-above-input stack (the pre-existing pattern,
  replaced everywhere it appeared for filters).
- **`components/v2/date-time-picker.tsx`** — `DateTimePicker` gained a `variant?:
  "default" | "compact"` prop (default unchanged, so every non-filter call site — forms/
  dialogs in `NewTaskForm.tsx`/`CreateTaskForm.tsx`/`new-conversation-dialog.tsx`/
  `conversation-panel.tsx`/`reminder-sms-dialog.tsx`/`task-tab.tsx`/
  `user-management-tab.tsx` — is untouched). `variant="compact"` (used only by the three
  filter bars' Due-from/to/From/To fields) renders a plain `<button>` styled like `Input`
  (`h-8`, `border-input`, no button shadow/hover-fill) with the display text left and a
  trailing 14px `CalendarIcon` — replacing the default's `Button variant="outline"` with a
  leading icon.
- **`TaskBoardPageV2.tsx`** — the old two-row filter layout (top row: Priority/Status/
  Assignee/Active `Select`s + Recurring-only toggle, mixed in with the Board/List `Tabs`
  and "New task" button; second row: Description/Patient/Due-from/Due-to) is now one
  `FilterBar` with all eight fields (Description, Patient, Due from, Due to, Status,
  Assignee, Priority, Active) laid out consistently; `Tabs`/"New task" stay on their own
  row above the grid (page-level controls, not filters). Recurring-only toggle + a new
  `Reset` button (clears every filter, including Priority/Active/Recurring, back to
  defaults — `dueFrom`/`dueTo` reset to `defaultFromDaysAgo(6)`/today) sit right-aligned
  below the grid. Note: Priority/Active/Recurring weren't in the original restyle request's
  field list but are pre-existing filters on this screen — kept in the grid rather than
  dropped, since this was scoped as layout-only.
- **`SmsHistoryPage.tsx`** — Patient/Sender/Phone/Text/Status/From/To (the filters that
  actually exist today) moved into a `FilterBar`; new `Reset` button right-aligned below it.
- **`AuditLogPageV2.tsx`** — Actor/Action/From/To moved into a `FilterBar`; new `Reset`
  button right-aligned below it. ("Actor (Admin only)" from the request is automatically
  satisfied — the whole page already gates non-Admins to an `EmptyState` before rendering
  any filters.)
- **Verification**: `npx tsc -b` clean (no type errors) across all three edited pages plus
  the two new/changed shared components. Could not verify visually against a live Docker
  stack or run `vite build` this session — this sandbox has no Docker, and the mounted
  `node_modules` is missing its Linux native binding for the project's Vite/Rolldown build
  (`Cannot find module '@rolldown/binding-linux-x64-gnu'`), a pre-existing environment
  mismatch unrelated to this change (the install was done on Windows). Recommend a real
  `docker compose up --build` click-through on all three screens (and at `lg`/`sm`/mobile
  widths) before treating this as fully verified.

---

## 7. Test structure

### Domain (`NotifyHub.Tests/NotifyHub.Domain.Tests/`) — no DB
Files: `PasswordPolicyTests.cs`, `TemplateRendererTests.cs`, `IdempotencyKeyGeneratorTests.cs`, `RetryBackoffPolicyTests.cs`, `OptOutKeywordMatcherTests.cs`, `TaskDueDateDefaultsTests.cs`, `RecurrenceCalculatorTests.cs`, `RateLimitCheckerTests.cs`, `QuietHoursCalculatorTests.cs` (last two added increment 10), `MessageExpiryCalculatorTests.cs` (added P9-07), `ReminderScheduleCalculatorTests.cs` (added P9-08), `PduSegmentCalculatorTests.cs` (added P9-09), `TaskForwardingRuleOverlapTests.cs` (added P9-10). `ReminderDueCalculatorTests.cs`/`ReminderTriggerReferenceTests.cs` deleted in P9-08 along with the classes they tested.
Run: `dotnet test NotifyHub.Tests/NotifyHub.Domain.Tests`

### Integration (`NotifyHub.Tests/NotifyHub.Integration.Tests/`)
| Test file | Factory |
|---|---|
| `AuthEndpointTests.cs`, `EscalationJobTests.cs`, `InboundWebhookTests.cs`, `MessageDispatcherOptOutTests.cs`, `TasksControllerTests.cs`, `ThreadsControllerTests.cs`, `AuditControllerTests.cs`, `TemplatesControllerTests.cs`, `UsersControllerTests.cs` (added increment 2 — create/assignable-filtering/auto-forward-on-status-change), `ActiveUserRequiredFilterTests.cs` (added increment 3 — mutating-request 403 for Inactive users, GET still works, login still works even after the JWT was issued while Active), `BookmarksControllerTests.cs` (added increment 8 — CRUD + role checks), `SettingsControllerTests.cs` (added increment 10 — get/update/role-check/system-info), scheduled-send/rate-limit tests added to `ThreadsControllerTests.cs` and a quiet-hours-skips-the-batch test added to `MessageDispatcherOptOutTests.cs` (both increment 10), `DashboardControllerTests.cs` (added increment 12 — own-vs-org task counts, unread-thread count), `PreviewTemplate_*` tests added to `ThreadsControllerTests.cs` (P9-04 — dummy-appointment-time fallback + real-upcoming-appointment resolution), `Update_Body_ClearsRenderedBody_OnQueuedMessagesLinkedToTemplate` added to `TemplatesControllerTests.cs` (P9-05 — also proves a non-`Queued` message's `RenderedBody` is left untouched), `MessagesControllerTests.cs` (P9-06 — 403 for Staff, `"System"` sender fallback, patient/sender/status/text filters), `MessageExpiryDispatchTests.cs` (P9-07 — expires an overdue `Queued` message before any send attempt, and proves expiry still runs during Quiet Hours even though the batch itself is suppressed), `RemindersTests.cs` (P9-08 — create/duplicate-409/past-time-400/update-recalculates/cancel/cancel-twice-400/cancel-non-reminder-400; `ReminderSchedulerTests.cs` deleted along with the class it tested), PDU-count assertions added to `MessagesControllerTests.cs` and `OutboundPipelineTests.cs`'s happy-path test (P9-09), `TaskForwardingRulesTests.cs` (P9-10 — self-target/inactive-target/overlap rejections, owner-only delete, `ResolveNewTaskAssigneeAsync`'s 4 resolution branches, end-to-end `CreateTask`-forwards-and-audits), `RevertExpiredLeaveAsync_*` tests added to `EscalationJobTests.cs` and `UpdateStatus_ToOnLeave_RequiresBothLeaveDates` added to `UsersControllerTests.cs` (P9-12) | `CustomWebApplicationFactory` (EF Core InMemory) |
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
