# NotifyHub — Documentation Index

This is the manifest required by the root `CLAUDE.md`: the first document consulted before
analyzing source for any feature. One tree entry per feature below, cross-referencing
frontend/backend/worker/database/security/business-rule/API/related-feature/dependency
documentation rather than duplicating it.

**Documentation priority** (when sources conflict — report the conflict, don't guess):
1. Approved Business Rules (`PROJECT_CONTEXT.md` BR- entries, explicit user instruction)
2. Functional Specification (`PROJECT_CONTEXT.md` FR- entries)
3. This manifest
4. Architecture documentation (`CODEBASE_MAP.md`)
5. Source code
6. Comments

**Coverage**: all implemented features are populated below — Inbox, Tasks, SMS, Users,
Templates (incl. Bookmarks), Settings, Audit Log, Dashboard, Auth. New features (none exist
outside these today) should be added the same way (see
`.claude/skills/notifyhub-docs/SKILL.md`).

---

## Inbox

```
Inbox
 ├── Frontend        → docs/Frontend/Inbox.md
 ├── Backend         → docs/Backend/Inbox.md
 ├── Worker          → none — inbound routing + unread-count updates happen synchronously in
 │                      ThreadsController/WebhooksController, no background job (CODEBASE_MAP.md §3)
 ├── Database        → ConversationThread, InboundMessage — CODEBASE_MAP.md §2
 ├── Security        → default authenticated policy + ActiveUserRequiredFilter; inbound webhook
 │                      is [AllowAnonymous][SharedSecret] — CODEBASE_MAP.md §3, docs/SECURITY.md
 ├── Business Rules  → FR-005, FR-006, FR-007, BR-001, BR-012
 ├── APIs            → ThreadsController (list/detail/reply/assign/create-conversation),
 │                      WebhooksController (inbound) — CODEBASE_MAP.md §3
 ├── Source folders  → NotifyHub.Api/Controllers/ThreadsController.cs,
 │                      NotifyHub.Api/Controllers/WebhooksController.cs,
 │                      notifyhub-web/src/pages/InboxPage.tsx,
 │                      notifyhub-web/src/components/inbox/, notifyhub-web/src/hooks/useThreads.ts,
 │                      notifyhub-web/src/hooks/useInboxHub.ts
 ├── Related         → Tasks (a thread's message can spawn a task via CreateTask),
 │                      SMS (Reply/CreateReminder both create OutboundMessage rows on a thread)
 └── Dependencies    → SignalR InboxHub, User (assignment/status via ActiveUserRequiredFilter)
```

---

## Tasks

```
Tasks
 ├── Frontend        → docs/Frontend/Tasks.md
 ├── Backend         → docs/Backend/Tasks.md
 ├── Worker          → docs/Worker/Escalation.md  (EscalationJob/EscalationWorker — see note)
 ├── Database        → TaskItem, TaskForwardingRule — CODEBASE_MAP.md §2
 ├── Security        → default authenticated policy; ActiveUserRequiredFilter rejects assigning
 │                      to a non-Active user — CODEBASE_MAP.md §3, docs/SECURITY.md
 ├── Business Rules  → FR-008, BR-004, BR-007, BR-014
 ├── APIs            → TasksController, ThreadsController.CreateTask,
 │                      TaskForwardingRulesController (P9-10) — CODEBASE_MAP.md §3
 ├── Source folders  → NotifyHub.Api/Controllers/TasksController.cs,
 │                      NotifyHub.Api/Controllers/TaskForwardingRulesController.cs,
 │                      NotifyHub.Infrastructure/Escalation/EscalationJob.cs,
 │                      NotifyHub.Worker/EscalationWorker.cs,
 │                      notifyhub-web/src/pages/TaskBoardPage.tsx,
 │                      notifyhub-web/src/components/tasks/, notifyhub-web/src/hooks/useTasks.ts
 ├── Related         → Inbox (tasks are created from a thread), Users (assignment, forwarding
 │                      rules, on-leave auto-forward)
 └── Dependencies    → User (assignee/original owner), ConversationThread (FK)
```

**Discrepancy flagged**: the requested tree format listed `Worker/Dispatcher.md` under Tasks.
Per `CODEBASE_MAP.md` §4, the Tasks-relevant worker is `EscalationJob`/`EscalationWorker`
(overdue-task escalation, plus P9-12's leave-revert); `MessageDispatcher` is SMS-specific
(§4b). Filed as `docs/Worker/Escalation.md` instead — code wins per this repo's own rule.

---

## SMS

```
SMS
 ├── Frontend        → docs/Frontend/SMS.md
 ├── Backend         → docs/Backend/SMS.md
 ├── Worker          → docs/Worker/Dispatcher.md  (DispatcherWorker/MessageDispatcher — see note)
 ├── Database        → OutboundMessage, DeliveryStatusHistory — CODEBASE_MAP.md §2
 ├── Security        → webhook/mock-gateway use [AllowAnonymous][SharedSecret]; opt-out
 │                      enforced server-side (BR-001) — CODEBASE_MAP.md §3, docs/SECURITY.md
 ├── Business Rules  → FR-001–004, FR-009, BR-001–003, BR-009–011, BR-013;
 │                      Reminder SMS rules 3/5–10/15–18/22/27–32/34 (STEP9_PLAN.md, CODEBASE_MAP.md §4b)
 ├── APIs            → MessagesController, ThreadsController (Reply/CreateReminder),
 │                      WebhooksController, MockGatewayController — CODEBASE_MAP.md §3, §4b
 ├── Source folders  → NotifyHub.Infrastructure/Messaging/MessageDispatcher.cs,
 │                      NotifyHub.Worker/DispatcherWorker.cs,
 │                      NotifyHub.Domain/Messaging/ReminderScheduleCalculator.cs,
 │                      notifyhub-web/src/components/v2/reminder-sms-dialog.tsx
 ├── Related         → Inbox (SMS sent/received within a thread)
 └── Dependencies    → SystemSetting (Quiet Hours, rate limit, reminder offsets), Patient (opt-out)
```

**Discrepancy flagged**: the requested tree format listed `Worker/Reminder.md` under SMS.
Per `CODEBASE_MAP.md` §4, `ReminderWorker`/`ReminderScheduler` were retired entirely in P9-08
— Reminder SMS now flows through the same `DispatcherWorker`/`MessageDispatcher` as Standard
SMS (rule 22: no parallel send path). Filed as `docs/Worker/Dispatcher.md` instead — code wins
per this repo's own rule.

---

## Users

```
Users
 ├── Frontend        → docs/Frontend/Users.md
 ├── Backend         → docs/Backend/Users.md
 ├── Worker          → none — auto-forward-on-deactivation runs synchronously inside
 │                      UsersController's status-PATCH (same SaveChangesAsync); the P9-12
 │                      leave-revert piggybacks on EscalationWorker's poll loop, see
 │                      docs/Worker/Escalation.md under Tasks
 ├── Database        → User (FullName, Status, LeaveFrom/LeaveTo), RefreshToken — CODEBASE_MAP.md §2
 ├── Security        → GET/POST api/users, PATCH status all [Authorize(Roles="Admin")];
 │                      GET api/users/assignable is default authenticated (Active users only)
 │                      — CODEBASE_MAP.md §3, docs/SECURITY.md
 ├── Business Rules  → PROJECT_CONTEXT.md §4 (roles/permissions), BR-005
 ├── APIs            → UsersController — CODEBASE_MAP.md §3
 ├── Source folders  → NotifyHub.Api/Controllers/UsersController.cs,
 │                      NotifyHub.Api/Users/ActiveUserRequiredFilter.cs,
 │                      NotifyHub.Infrastructure/Users/FallbackUserResolver.cs,
 │                      notifyhub-web/src/components/settings/user-management-tab.tsx,
 │                      notifyhub-web/src/hooks/useUsers.ts
 ├── Related         → Tasks (assignment, forwarding rules, escalation fallback), Inbox
 │                      (assignment-target validation), Auth (RBAC roles)
 └── Dependencies    → TaskForwardingRule, TaskItem (mass-reassignment on deactivation)
```

---

## Templates

Includes Bookmarks (admin-curated merge-field snippets) as a sub-feature — see
`docs/Frontend/Templates.md`/`docs/Backend/Templates.md` for why they're not a separate entry.

```
Templates
 ├── Frontend        → docs/Frontend/Templates.md
 ├── Backend         → docs/Backend/Templates.md
 ├── Worker          → none — template edits are propagated synchronously (P9-05 sweep) and
 │                      re-rendered at dispatch time by MessageDispatcher, see
 │                      docs/Worker/Dispatcher.md under SMS
 ├── Database        → MessageTemplate, Bookmark — CODEBASE_MAP.md §2
 ├── Security        → default authenticated for Templates CRUD; Bookmarks CRUD is
 │                      [Authorize(Roles="Admin")] except GET — CODEBASE_MAP.md §3, docs/SECURITY.md
 ├── Business Rules  → FR-001, BR-013
 ├── APIs            → TemplatesController, BookmarksController,
 │                      ThreadsController.PreviewTemplate (P9-04) — CODEBASE_MAP.md §3
 ├── Source folders  → NotifyHub.Api/Controllers/TemplatesController.cs,
 │                      NotifyHub.Api/Controllers/BookmarksController.cs,
 │                      NotifyHub.Domain/Messaging/TemplateRenderer.cs,
 │                      notifyhub-web/src/pages/TemplatesPage.tsx,
 │                      notifyhub-web/src/components/v2/template-form.tsx,
 │                      notifyhub-web/src/components/v2/merge-field-text.tsx,
 │                      notifyhub-web/src/components/settings/template-tab.tsx
 ├── Related         → SMS (dispatch renders templates), Inbox (composer preview/insert)
 └── Dependencies    → OutboundMessage.TemplateId, Bookmark
```

---

## Settings

Covers the shared `SettingsPage.tsx` shell + the tabs with no other home (General,
Notification, System). SMS/Task/Template/User-Management tab logic is documented under its
owning feature (SMS, Tasks, Templates, Users respectively) and only cross-referenced here.

```
Settings
 ├── Frontend        → docs/Frontend/Settings.md
 ├── Backend         → docs/Backend/Settings.md
 ├── Worker          → none
 ├── Database        → SystemSetting (generic key-value store) — CODEBASE_MAP.md §2
 ├── Security        → GET api/settings/system-info default authenticated (read-only diagnostics);
 │                      PATCH api/settings is [Authorize(Roles="Admin")] — CODEBASE_MAP.md §3, docs/SECURITY.md
 ├── Business Rules  → PROJECT_CONTEXT.md §3 "Explicitly dropped" (Quiet Hours/rate limiting
 │                      were stretch goals, built anyway in increment 10 — see STATUS.md)
 ├── APIs            → SettingsController — CODEBASE_MAP.md §3
 ├── Source folders  → NotifyHub.Api/Controllers/SettingsController.cs,
 │                      NotifyHub.Infrastructure/Settings/SettingsService.cs,
 │                      notifyhub-web/src/pages/SettingsPage.tsx,
 │                      notifyhub-web/src/components/settings/general-tab.tsx,
 │                      notifyhub-web/src/components/settings/notification-tab.tsx,
 │                      notifyhub-web/src/components/settings/system-tab.tsx,
 │                      notifyhub-web/src/hooks/useSettings.ts
 ├── Related         → SMS (Quiet Hours/rate limit/reminder defaults), Tasks (forwarding rules,
 │                      due-date defaults display), Templates (Bookmark CRUD), Users
 │                      (User Management tab)
 └── Dependencies    → SystemSetting, SettingsService (typed accessors)
```

---

## Audit Log

```
Audit Log
 ├── Frontend        → docs/Frontend/AuditLog.md
 ├── Backend         → docs/Backend/AuditLog.md
 ├── Worker          → none — every controller writes AuditLog rows synchronously at the point
 │                      of action, no batching/async job
 ├── Database        → AuditLog (polymorphic EntityType/EntityId, no FK) — CODEBASE_MAP.md §2
 ├── Security        → GET api/audit is [Authorize(Roles="Admin")] (first non-default,
 │                      non-webhook auth policy in the codebase); GET api/audit/mine is default
 │                      authenticated, server-hardcoded to the caller — CODEBASE_MAP.md §3, docs/SECURITY.md
 ├── Business Rules  → FR-011 (5 explicit event types: send, delivery receipt, opt-out, thread
 │                      assignment, task escalation — a subset of the actual action types, see
 │                      docs/Backend/AuditLog.md)
 ├── APIs            → AuditController — CODEBASE_MAP.md §3
 ├── Source folders  → NotifyHub.Api/Controllers/AuditController.cs,
 │                      NotifyHub.Domain/Entities/AuditLog.cs,
 │                      notifyhub-web/src/pages/AuditLogPage.tsx,
 │                      notifyhub-web/src/components/v2/status-config.ts (AUDIT_ACTION_CONFIG)
 ├── Related         → every feature (write side); Dashboard (recent-activity feed reuses this
 │                      data + its status config)
 └── Dependencies    → none (read/write target for all other features)
```

---

## Dashboard

```
Dashboard
 ├── Frontend        → docs/Frontend/Dashboard.md
 ├── Backend         → docs/Backend/Dashboard.md
 ├── Worker          → none
 ├── Database        → none dedicated — pure read-side aggregation over TaskItem,
 │                      ConversationThread, AuditLog — CODEBASE_MAP.md §2
 ├── Security        → default authenticated; OrgTasks field is null for non-Admins
 │                      (server-side scoping, not just hidden UI) — CODEBASE_MAP.md §3, docs/SECURITY.md
 ├── Business Rules  → not a numbered FR — added increment 13 as a post-login summary screen
 ├── APIs            → DashboardController — CODEBASE_MAP.md §3
 ├── Source folders  → NotifyHub.Api/Controllers/DashboardController.cs,
 │                      notifyhub-web/src/pages/DashboardPage.tsx,
 │                      notifyhub-web/src/components/v2/task-nav-widget.tsx,
 │                      notifyhub-web/src/hooks/useDashboard.ts
 ├── Related         → Tasks (task counts), Inbox (unread count), Audit Log (recent activity)
 └── Dependencies    → TaskItem, ConversationThread, AuditLog (read-only)
```

---

## Auth

```
Auth
 ├── Frontend        → docs/Frontend/Auth.md
 ├── Backend         → docs/Backend/Auth.md
 ├── Worker          → none
 ├── Database        → RefreshToken (unique index TokenHash, index UserId) — CODEBASE_MAP.md §2
 ├── Security        → login/refresh/logout are [AllowAnonymous]; global AuthorizeFilter
 │                      requires authentication everywhere else by default; ActiveUserRequiredFilter
 │                      layers a live-Status check on top — CODEBASE_MAP.md §3, docs/SECURITY.md,
 │                      docs/adr/0003-rbac-model.md
 ├── Business Rules  → PROJECT_CONTEXT.md §4 (roles/permissions), FR-018(a), BR-005
 ├── APIs            → AuthController — CODEBASE_MAP.md §3
 ├── Source folders  → NotifyHub.Api/Controllers/AuthController.cs,
 │                      NotifyHub.Api/Extensions/AuthServiceCollectionExtensions.cs,
 │                      notifyhub-web/src/pages/LoginPage.tsx,
 │                      notifyhub-web/src/context/AuthContext.tsx,
 │                      notifyhub-web/src/lib/tokenStore.ts,
 │                      notifyhub-web/src/routes/ProtectedRoute.tsx
 ├── Related         → Users (RBAC roles, Active-status gate), every feature (global auth filter)
 └── Dependencies    → RefreshToken, User (credentials/role/status)
```
