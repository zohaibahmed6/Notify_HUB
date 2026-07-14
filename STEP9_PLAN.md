# NotifyHub — Step 9 Plan

*Informal requirements list, not FR-numbered — same pattern as Step 8 (see `STATUS.md`). Locked
via a planning session with Zohaib on 2026-07-14; this file is the source of truth for that
session's decisions. Read this file plus `CODEBASE_MAP.md` before touching code — do not re-derive
scope from chat history.*

**Build convention (same as Step 8):** implement as independently-committed increments, each with
its own migration/tests where applicable. Update the relevant section of `CODEBASE_MAP.md` /
`STATUS.md` / `PROJECT_CONTEXT.md` as part of the same commit that ships an increment — not a
separate follow-up task. Flag any ambiguity found during implementation rather than silently
resolving it (standing instruction, `CLAUDE.md`).

**Superseded/reversed decisions from this session** (do not treat prior locked decisions below as
still active):
- PROJECT_CONTEXT.md §14 "Reminder scheduler interval: 15 minutes" — **moot**, not overridden. The
  entire automatic `ReminderScheduler`/`ReminderWorker` system is retired in P9-10 below, so there
  is no more reminder poll interval to tune.
- `MessageTemplate.OffsetHours` and its use for `TriggerType=AppointmentReminder` — **removed from
  the Templates UI** (P9-01e). Superseded by P9-10's `ReminderOffset`/`ReminderExpiryOffset`
  Practice Settings.

---

## P9-00 — Responsive design (build first, before P9-01)

| Decision | Detail |
|---|---|
| Scope | Redesign (v2) screens only — legacy pages are unreachable dead code since the UI toggle was removed in increment 11, not worth making responsive |
| Approach | Reflow existing components with responsive Tailwind breakpoints (sm/md/lg/xl, already available — no new breakpoint system needed). Same components, layout adapts per screen width — not a separate simplified mobile build |
| Content rule (explicit, verbatim intent) | **All content must remain visible/reachable at every screen size — nothing gets dropped or hidden on mobile, only rearranged.** Stricter than a typical "hide low-priority info on small screens" pattern |
| Two-pane screens (`InboxPageV2`: `ThreadList`+`ConversationPanelV2`) | Single-pane on mobile with back-navigation between the two, instead of squeezing both side-by-side |
| Tables (`AuditLogPageV2`, `TemplatesPageV2`'s split pane, P9-06's SMS History) | Stacked card rows on mobile instead of horizontally-scrolling tables — every column's data still visible, just vertical per row |
| Nav (`AppShell` top nav) | Collapses into a hamburger/drawer below a breakpoint |
| Kanban board (`TaskBoardPageV2`) | Fall back to its existing "List" tab view on small screens, or horizontal-scroll the board — every status column's tasks must stay reachable, not hidden |
| Modals/Sheets (`TaskDetailSheet`, dialogs) | Already shadcn `Sheet`/`Dialog` — verify no overflow/clipping at narrow widths, fix if found |
| Dashboard stat cards | Stack vertically instead of a grid row on mobile |
| New Step 9 screens/components | SMS History, Reminder SMS modal, `DateTimePicker` (P9-03) inherit these same patterns once built — this is why P9-00 is sequenced before P9-01, so nothing gets retrofitted twice |

---

## P9-01 — Quick fixes (no schema changes, build first)

| ID | Change | Files | Decision |
|---|---|---|---|
| P9-01a | Remove the Cmd+K search/command palette entirely | `notifyhub-web/src/components/layout/AppShell.tsx` (paletteOpen state, keydown listener, header trigger button), `notifyhub-web/src/components/v2/command-palette.tsx` (delete or fully unwire) | Whole feature removed — button, shortcut, `CommandPalette` component, "New task"/"New template" quick actions. Not just hiding the button. |
| P9-01b | Task Forward dialog excludes the currently-assigned staff member from the target list | `components/v2/task-detail-sheet.tsx` (Forward dialog, `useAssignableUsers()`-backed Select) | Filter `task.assignedStaffId` out client-side. Only the current assignee, not the original owner. |
| P9-01c | Task detail modal (`TaskDetailSheet`) auto-closes after: forward, complete, active/inactive toggle, assign-to-me, **or any other reassignment** | `components/v2/task-detail-sheet.tsx` | Scope is "any action taken on the task from the sheet," not just the 4 originally named actions. |
| P9-01d | Task creation form: every field required except Description (optional, server auto-populates from last thread message when blank per existing FR-008/increment-5 behavior) and time (optional, defaults `00:00`) | `components/tasks/NewTaskForm.tsx`, `components/inbox/CreateTaskForm.tsx` | Split the due-date input into date (required) + time (optional) before combining into the `DueAt` payload. |
| P9-01e | Remove the `OffsetHours` field from the Templates screen UI | `TemplateForm` (`components/v2/template-form.tsx`), `TemplatesPageV2` | Backend `MessageTemplate.OffsetHours` column can stay (avoid a breaking migration) but stop reading/writing it from the UI — it's dead once P9-10 ships. |

---

## P9-02 — SignalR delivery-status "double tick"

**Root cause:** no SignalR broadcast currently fires when `WebhooksController.GatewayReceipt`
updates a message's delivery status — only creation-time events (`outboundMessageSent`) broadcast
today. That's the actual bug, not a frontend rendering issue.

| Decision | Detail |
|---|---|
| New event | `messageStatusUpdated { threadId, messageId, status }`, broadcast from `WebhooksController.GatewayReceipt` after the DB write, same pattern as the existing `outboundMessageSent`/`threadAssigned` broadcasts (`ThreadsController.cs:127`/`:161`) |
| Frontend listener | `useInboxHub.ts` — new listener alongside the existing 3 (`:26`/`:31`/`:36`), invalidates `["thread", threadId]` |
| Status → icon mapping | `Queued` = clock icon, `Sent` = single tick, `Delivered` = double tick |
| UX requirement | Updates live in place — no page refresh, no thread switch required. Verify the existing `StatusBadge`/`DELIVERY_STATUS_CONFIG` (`status-config.ts`) re-renders correctly off the invalidated cache. |

---

## P9-03 — Calendar / date-time picker redesign

Reference: Zohaib's provided image (Material-style date card + clock-face time picker).

| Decision | Detail |
|---|---|
| Scope | One shared `DateTimePicker` component, swapped into **every** date/datetime input in the app — not per-screen variants |
| New dependency | shadcn `calendar` primitive (`npx shadcn add calendar`) — not in the project today; current primitives list (`dialog`/`dropdown-menu`/`sheet`/`command`/`select`/`popover`/`table`/`skeleton`/`avatar`/`tooltip`/`tabs`/`separator`/`scroll-area`) has no date picker |
| Replaces native inputs at | `NewTaskForm.tsx`, `CreateTaskForm.tsx`, `NewConversationDialog` (schedule field), `ConversationPanelV2` (Schedule toggle), `TaskBoardPageV2` filter bar (Due from/to), `AuditLogPage.tsx`/`AuditLogPageV2.tsx` (date range) |

---

## P9-04 — Template bookmarks resolve to real values in the composer

| Decision | Detail |
|---|---|
| Behavior | When staff selects a template in `ConversationPanelV2`'s composer, `{{patient_name}}` resolves to the thread's actual `Patient.Name`; `{{appointment_time}}` resolves to a real appointment if one exists for that patient, else a generated future dummy time |
| Editability | Resolved text fills the composer textbox and is **editable** — staff can tweak before sending, same as any ad-hoc reply. Not a locked preview. |
| Server-side send-time rendering (BR-013) | Unchanged — `rendered_body` is still snapshotted at actual dispatch time regardless of what was shown in the composer |
| Files | `TemplateRenderer.cs` (currently only resolves at send time — needs a preview-resolve path too), `ConversationPanelV2`'s "Insert template" dropdown |

---

## P9-05 — Template edits propagate to already-queued messages

**Dual-safety, per Zohaib's explicit call ("handle at both ends... so no wrong SMS could send")**

| Decision | Detail |
|---|---|
| On template save | Explicit sweep: any `Queued` `OutboundMessage` whose `TemplateId` matches the edited template gets its pending render source updated to the new body |
| At dispatch time | `MessageDispatcher`'s existing render-at-dispatch step (`DispatchOneAsync`, renders template if set) stays as the second safety net — confirm in code whether `RenderedBody` is already null until dispatch for template-linked messages (likely, per current architecture) so this may already partially work; verify, don't assume |
| Files | `TemplatesController.cs` (`Update`), `MessageDispatcher.cs` |

---

## P9-06 — SMS History report (new screen)

| Field | Detail |
|---|---|
| Access | Admin-only, new top-nav link (matches `GET /api/audit`'s access pattern, not the shared-inbox model) |
| Columns | Patient name, staff username (sender — "System" for templated/system sends), phone number, SMS text (`RenderedBody`), Status (Queued/Sending/Sent/Delivered/Failed/**Expired**), **Scheduled Time**, **Expiry Time**, **PDU Count** |
| PDU Count display | `—`/pending while `Status` is Queued/Sending (no receipt yet); populated once a delivery receipt lands (see P9-11) |
| Filters | All columns filterable — patient name, username, phone, text (substring), status, date range |
| Pagination | Required — reuse the existing `PagedResult<T>`/`Clamp` pattern used by every other list endpoint |
| Summary row | Above the table, scoped to the **current date filter**: Total SMS count (all rows matching the filter, any status) + Total PDU count (sum of `PduCount` across rows matching the filter; rows with no receipt yet contribute 0) |
| New endpoint | `GET /api/messages` (or similar) — new `MessagesController` or extend an existing one; check `CODEBASE_MAP.md` §3 for the closest existing pattern before creating a new controller |

---

## P9-07 — Message expiry engine (Standard SMS)

| Rule | Detail |
|---|---|
| New terminal status | `MessageStatus.Expired` — 6th value alongside the existing Queued/Sending/Sent/Delivered/Failed/Superseded, never picked up by the dispatcher's `Status == Queued` query once set (same pattern as `Superseded`) |
| New columns | `OutboundMessage.ExpiresAt` (DateTime), `OutboundMessage.ExpiryReason` (string?, nullable) |
| Default expiry | 12 hours, from `CreatedAt` for immediate sends, from `ScheduledAt` for scheduled sends |
| Where checked | Folded into the existing `DispatcherWorker` poll loop — same query pass that already finds due messages now also marks `Expired` any `Queued` message where `ExpiresAt < now`, before attempting to send |
| Reason text | Generated labels per cause (e.g. "Message expired due to quiet hours", "Message expired — scheduled window passed") — exact wording decided at build time, not pre-specified |
| Interaction with Quiet Hours | Since Quiet Hours gates the whole dispatch batch without touching attempt count, this is the realistic path that triggers expiry in practice — BR-011's own retry/backoff (max ~31 min across 6 attempts) almost never reaches a 12h expiry on its own |
| Report visibility | `Expired` is one of the status filter options in P9-06's SMS History screen |

---

## P9-08 — Reminder SMS engine (replaces the automatic Appointment-based system)

**This retires `ReminderScheduler`/`ReminderWorker` and the `Appointment`-polling reminder flow
entirely** (`NotifyHub.Worker/ReminderWorker.cs`, `NotifyHub.Infrastructure/Reminders/*` — delete,
don't just stop calling). Confirmed explicitly by Zohaib: full retirement, not parallel operation.
Per rule 31 below, the new engine is generic and must not couple to the `Appointment` entity.

### UI entry point
New "Reminder SMS" action inside a conversation thread (same discoverability tier as the existing
"Insert template" composer action) — clicking it opens a modal, not an inline composer change.
Modal fields: template/message selection (reuses the same picker infra as P9-04's composer) +
**Event Time** picker (uses P9-03's `DateTimePicker`). No manual "scheduled send time" field — that's
computed server-side (rule 4/29 below).

### Business rules (verbatim from Zohaib, minus items marked N/A)

1. The system supports two SMS types: Standard SMS and Reminder SMS.
2. Standard SMS may be sent immediately or scheduled for a future date and time.
3. Reminder SMS are event-based and require an Event Date & Time.
4. Users cannot manually enter the Scheduled Send Time for Reminder SMS.
5. `Scheduled Send Time = Event Time − Reminder Offset`.
6. Default Reminder Offset = 1440 minutes (24h), configurable in Settings → SMS (reusing the
   existing `SystemSetting`/`SettingsService` key-value pattern that already backs Quiet Hours and
   rate limiting).
7. A changed Reminder Offset applies only to newly created Reminder SMS, not retroactively.
8. Reminder SMS must not be created if the calculated Scheduled Send Time is already in the past.
9. Minimum selectable Event Time in the UI = Current Time + Reminder Offset.
10. UI must prevent selecting an Event Time that violates rule 9.
11. Standard SMS: empty Scheduled Send Time = send immediately; provided = wait until that time.
12. Standard SMS default expiry = 12 hours (P9-07, unchanged by this section).
13. Standard SMS immediate: expiry = Created Time + 12h.
14. Standard SMS scheduled: expiry = Scheduled Send Time + 12h.
15. Reminder SMS expiry = `Event Time − Reminder Expiry Offset`.
16. Default Reminder Expiry Offset = 15 minutes before Event Time, configurable (same Settings →
    SMS location as rule 6).
17. Reminder SMS expiry is never calculated from Created Time.
18. Reminder SMS expiry is never calculated from Scheduled Send Time.
19. Both Scheduled Send Time and Expiry Time are calculated and stored at creation.
20. Expired SMS must never be sent.
21. Dispatcher verifies non-expiry immediately before every send attempt (same check point as
    P9-07's expiry sweep — one shared mechanism, not two).
22. Reminder SMS use the same queue, dispatcher, retry policy, gateway, and delivery tracking as
    Standard SMS — no parallel send path.
23. ~~API/integrations pass-through~~ — **N/A**, confirmed by Zohaib. The whole app is already "the
    API"; no separate integration surface exists.
24. ~~Bulk import~~ — **N/A**, confirmed. No bulk-import feature exists in NotifyHub.
25. Users cannot manually edit the calculated Scheduled Send Time or Expiry Time for Reminder SMS —
    server-computed only (ties to rule 29).
26. If Event Time changes before send, Scheduled Send Time and Expiry Time are automatically
    recalculated.
27. If a Reminder SMS has already been sent, its Event Time cannot be changed.
28. Users may cancel a Reminder SMS before it's sent — **new capability, no cancel endpoint exists
    today**. Build `POST /api/messages/{id}/cancel` (or similar), only valid while `Status=Queued`.
29. Cancelled or expired Reminder SMS must never be processed.
30. Prevent duplicate Reminder SMS for the same (recipient, template, event time, reminder offset)
    combination — extend `IdempotencyKeyGenerator` with a Reminder-specific hash input (patientId +
    templateId + eventTime + reminderOffset), separate from Standard SMS's existing
    (patientId+templateId+triggerReference) hash.
31. All reminder calculations happen server-side only. UI may display calculated values for preview
    only (read-only, never accepts a client-supplied Scheduled/Expiry time).
32. Store per Reminder SMS: Event Time, Reminder Offset, Reminder Expiry Offset, Scheduled Send
    Time, Expiry Time, Created Time, Sent Time, Status.
33. ~~Multi-tenant orgs~~ — **N/A**, confirmed. NotifyHub has no multi-tenancy.
34. The Reminder Engine must be generic and independent of the `Appointment` module — reusable for
    future event-based reminders (payments, document expiry, renewals, follow-ups), not hardcoded to
    appointments. **This is why the old `Appointment`-polling `ReminderScheduler` is retired rather
    than extended.**

### Schema
New fields needed on `OutboundMessage` (or a new related entity if cleaner — decide at build time):
`EventTime`, `ReminderOffsetMinutes`, `ReminderExpiryOffsetMinutes`, `ScheduledSendTime` (may reuse
existing `ScheduledAt`), `ExpiryTime` (shared with P9-07's `ExpiresAt`?), `SentTime`. Reconcile
naming with P9-07's columns rather than duplicating — `ExpiresAt`/`ExpiryReason` from P9-07 likely
serve both Standard and Reminder SMS (rule 22 says they share the same pipeline).

---

## P9-09 — PDU (segment) count

**Source: the provider's delivery receipt, not computed by NotifyHub's own dispatcher** — mirrors
how real carrier APIs (e.g. Twilio) return segment counts in their status webhooks.

| Step | Detail |
|---|---|
| 1 | Mock gateway (`MockGatewayController.Send` — stands in for a real carrier, per project scope) computes a PDU count from the rendered SMS text using standard segmentation math: GSM-7 encoding if every character fits the GSM 03.38 alphabet, else UCS-2. Single-segment limits: GSM-7=160 chars, UCS-2=70 chars. Multi-segment limits (7/3 bytes reserved for concatenation header): GSM-7=153 chars/segment, UCS-2=67 chars/segment. `SegmentCount = 1` if length ≤ single-segment limit, else `ceil(length / multi-segment limit)`. |
| 2 | Included as a new `pduCount` field when the mock gateway POSTs its receipt to `api/webhooks/gateway-receipt` |
| 3 | `WebhooksController.GatewayReceipt` persists it to `OutboundMessage.PduCount` (nullable int) when the receipt lands |
| 4 | Before a receipt arrives (`Queued`/`Sending`), `PduCount` stays null — P9-06's report shows a pending placeholder |
| 5 | Immutable once set — snapshotted from the receipt, never recalculated later (same audit-integrity principle as `RenderedBody`/BR-013) |
| 6 | Shown **only** in the SMS History report (P9-06) — not surfaced live in the conversation thread view, per Zohaib's explicit call |
| 7 | P9-06's summary row includes Total PDU count for the active date filter (sum of non-null `PduCount` in range) |

---

## P9-10 — Task forwarding rules

New per-user "forward my tasks to X" configuration, checked before the existing always-fallback-to-
Admin logic — **does not replace it**.

### Business rules

1. New tasks created while the original assignee is Inactive check the forwarding rule first: if a
   valid active rule exists, assign to its target; otherwise assign to Admin (unchanged fallback).
2. **Existing-task behavior is unchanged**: when a user's Status transitions to Inactive/OnLeave,
   their existing non-terminal tasks still forward straight to the fallback Admin (today's
   `UsersController.PATCH /api/users/{id}/status` behavior) — the forwarding rule does **not**
   apply to this mass-reassignment event, only to new task creation going forward.
3. Forwarding target can be any Active user.
4. No overlapping forwarding rules for the same user (date-ranged rules, rule 8 below).
5. One level of forwarding only — no chaining (A→B, B→C not supported). If the rule's target is
   itself inactive, skip straight to Admin fallback.
6. If the forwarding target is inactive, disabled, or deleted at resolution time, ignore the rule
   and assign to Admin.
7. A user cannot set themselves as their own forwarding target.
8. Forwarding rules may be future-dated (From/To) and activate/deactivate automatically based on
   those dates — same date-range mechanism as P9-12's leave dates, but this is a separate concept
   (a rule's own active window, not the user's leave period).
9. Multiple future rules for the same user allowed only if their date ranges don't overlap (rule 4).
10. Rules can be edited/deleted at any time; changes affect future assignments only, never
    retroactively.
11. Store both Original Assignee (`original_owner_id`, already exists) and Current Assignee
    (`assigned_staff_id`, already exists) — no new columns needed here, existing schema covers it.
12. Every rule-based forward is audited (`action:"forward"`, actor `"system"`) — same convention as
    today's auto-forward-on-deactivation audit entries.
13. Forwarding never modifies due date, SLA, reminders, escalation timers, or status — assignee only.
14. A forwarding reason is optional.
15. Forwarding/audit history is permanently retained, not deleted when a rule expires.
16. Manual assignment (Admin directly picking someone via existing paths) does not bypass or modify
    forwarding rules for future assignments — they're independent mechanisms.

### Explicitly scoped down from the original 27-rule draft
- **No centralized "Assignment Engine" refactor.** Extend `FallbackUserResolver` (already shared by
  `EscalationJob` and the status-change path) to check the forwarding rule first — recurring-task
  spawn, manual PATCH reassignment, and the `Forward` endpoint keep their current logic untouched.
- Rules referencing API integrations, bulk import, and multi-tenant orgs — **N/A**, none of those
  features exist in NotifyHub.

### Where configured
New "Forward to" configuration in **Settings → Task tab** (`task-tab.tsx`, currently a read-only
`TaskDueDateDefaults` display — gains a real control here).

### Schema
New `TaskForwardingRule` entity: `UserId` (FK), `TargetUserId` (FK), `From`/`To` (nullable DateTime,
rule 8), unique/overlap constraint per `UserId`.

---

## P9-11 — Recurring task configuration UI

Backend engine already exists (`RecurrenceCalculator.cs`, BR-007) — this is frontend-only.

| Decision | Detail |
|---|---|
| Scope | "Recurring" toggle on `NewTaskForm.tsx`/`CreateTaskForm.tsx` only → reveals Interval (days, required if toggled), End date (optional), Max occurrences (optional) |
| Editability | **Creation-time only** — matches how the backend already works (next occurrence auto-spawns on completion via `SpawnNextOccurrenceIfDue`; no edit-after-creation path exists or is being added) |
| Verify before building | Confirm in code whether `ThreadsController.CreateTask`'s request DTO already accepts `IsRecurring`/`RecurrenceIntervalDays`/`RecurrenceEndDate`/`RecurrenceMaxOccurrences` — `CODEBASE_MAP.md` only confirms `Description`/`TaskType` were explicitly added at the API layer for these forms |

---

## P9-12 — User On-Leave From/To dates

| Decision | Detail |
|---|---|
| New columns | `User.LeaveFrom`, `User.LeaveTo` (nullable DateTime) |
| Validation | Both required together when `Status` is set to `OnLeave` via the User Management tab |
| Auto-revert | Once `LeaveTo` passes, `Status` auto-reverts `OnLeave → Active` — piggyback on the existing `EscalationWorker` poll rather than a new worker process |
| Files | `User.cs`, `UserConfiguration.cs`, `user-management-tab.tsx`, `EscalationWorker.cs` |

---

## Build order recommendation

P9-00 → P9-01 → P9-02 → P9-03 → P9-04 → P9-05 → P9-06 (skeleton, PDU/scheduled/expiry columns added
once P9-07/09 exist) → P9-07 → P9-08 → P9-09 → P9-10 → P9-11 → P9-12. P9-00 goes first so every
subsequent increment's new UI is built responsive from the start, not retrofitted. P9-06 is listed
early for screen scaffolding but its PDU/Scheduled/Expiry columns depend on P9-07/P9-08/P9-09
shipping first — build the table shell whenever convenient, wire the dependent columns last.


Read CLAUDE.md → CODEBASE_MAP.md → STEP9_PLAN.md (project root). STEP9_PLAN.md is a locked, pre-approved Step 9 spec (P9-00 responsive design + increments P9-01–P9-12) — treat as source of truth, don't ask me to re-confirm decisions already recorded there.

Build P9-00 first, then P9-01→P9-12 in the file's listed build order, one increment at a time. Per increment: implement, add/update tests where applicable, verify against the live Docker stack (not just compile-check), update CODEBASE_MAP.md/STATUS.md/PROJECT_CONTEXT.md in the same commit (match Step 8's documentation style in STATUS.md), commit independently.

Operate fully autonomously: never ask for build/run/bash/execution approval or any other permission question — just execute. Don't ask anything irrelevant to STEP9_PLAN.md's functional requirements, and don't assume or invent any requirement not specified there. Only stop and ask if there's genuine confusion about a business/functional requirement itself that STEP9_PLAN.md doesn't already resolve — never invent a decision in that case.