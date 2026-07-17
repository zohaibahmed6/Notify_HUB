# NotifyHub — Decision Log

Chronological log of architectural, technical, and business decisions, as required by the root
`CLAUDE.md`. This log starts clean from the date of its creation — historical decisions are
**not** reconstructed here; `STATUS.md` and `docs/adr/*.md` already cover project history up to
this point.

For a decision architecturally significant enough to warrant a full write-up (rejected
alternatives, broader impact), add a numbered ADR under `docs/adr/` instead/as well (see
`0001`–`0003` for the format) and link it from the entry here rather than duplicating its
content. Use this log for everything else, including smaller technical and business calls.

Newest entries at the top. Append a new entry each time a decision is made — don't rewrite or
remove prior entries.

---

## 2026-07-18 — Added `TaskItem.AssignedAt` so the top-nav task badge can sort by most-recently-assigned

**Decision:** Added a new nullable `TaskItem.AssignedAt` column (migration
`20260717122228_AddTaskAssignedAt`), stamped with `DateTime.UtcNow` at every point
`AssignedStaffId` is actually set to a new value — task creation (`ThreadsController.CreateTask`),
`TasksController.Update`'s `AssignedStaffId` branch (only when the value actually changes,
guarded the same way as that branch's existing `taskAssignmentChanged` SignalR broadcast),
`TasksController.Forward`, recurrence spawn (`SpawnNextOccurrenceIfDue`), `EscalationJob`'s
fallback-admin reassignment, and `UsersController`'s auto-forward-on-deactivation/leave sweep.
Added a new `TasksController.ApplySort` case (`sortBy=assignedAt`), and
`TaskNavWidget` (the top-nav task badge popover) now requests
`sortBy: "assignedAt", sortDir: "desc"` instead of the previous no-sort-param default
(soonest-`DueAt`-first).

**Reason:** Zohaib wanted the badge's dropdown to list the most recently assigned task first,
not soonest-due-first. No existing field captured "when was this task assigned" — `TaskItem`
had no `CreatedAt`/`AssignedAt` at all, and audit logs don't fill the gap either (the plain
PATCH assignment path has never been audited, a pre-existing gap). A genuine schema addition
was the only option.

**Alternatives considered:** Reusing/backfilling from audit logs — rejected, incomplete
coverage (PATCH assignments were never audited) so it can't be a reliable sort key.
Stamping `AssignedAt` unconditionally on every `Update` PATCH regardless of whether
`AssignedStaffId` actually changed — rejected, would let an unrelated field edit (e.g. a
priority change) spuriously bump a task to the top of "recently assigned," which isn't what
it means.

**Impacted modules:** Tasks (`NotifyHub.Domain/Entities/TaskItem.cs`,
`NotifyHub.Api/Tasks/Dtos/TaskDto.cs`, `TasksController`, `ThreadsController.CreateTask`,
`EscalationJob`, `UsersController`), Frontend (`types/tasks.ts`, `task-nav-widget.tsx`).

---

## 2026-07-18 — Admin users can no longer be set to Inactive or On Leave via User Management

**Decision:** `UsersController.UpdateStatus` (`NotifyHub.Api/Controllers/UsersController.cs`)
now rejects (400) any status-change request that targets a user with `Role == Admin` and a
requested status other than `Active` — Admin users can only ever be `Active`. The frontend
(`notifyhub-web/src/components/settings/user-management-tab.tsx`) mirrors this by disabling
the Inactive/OnLeave options in the per-row status `Select` for Admin rows, with a tooltip
explaining why, so the doomed request is never sent in the first place.

**Reason:** Zohaib asked that Admin accounts not be sidelined via User Management the way
Staff accounts can be — Admins are relied on as the fallback assignee for auto-forwarded and
escalated tasks (`FallbackUserResolver`), so an Admin should always remain `Active`.

**Alternatives considered:** 403 instead of 400 — rejected for consistency with this
controller's existing validation-style 400s (the OnLeave date-pairing checks in the same
method), keeping 403 reserved for the endpoint's `[Authorize(Roles="Admin")]` authorization
gate. Silently coercing the request to a no-op instead of rejecting it — rejected, an explicit
error is clearer than a silent no-op that could mask a caller's mistaken assumption.

**Impacted modules:** User Management (`NotifyHub.Api/Controllers/UsersController.cs`,
`notifyhub-web/src/components/settings/user-management-tab.tsx`,
`NotifyHub.Tests/NotifyHub.Integration.Tests/UsersControllerTests.cs`).
`CODEBASE_MAP.md` updated in the same change.

## 2026-07-17 — User display standardized to "FullName (Role)" everywhere, backed by new DTO fields

**Decision:** Every place the app shows "who this is" — assignee/provider dropdowns (task
creation, forwarding, filters), the "signed in as" header (`AppShell.tsx`, `general-tab.tsx`,
`TaskAssignmentFields.tsx`), and task/thread assignee labels in cards, lists, and detail views —
now renders a consistent `"FullName (Role)"` string via one shared frontend helper,
`notifyhub-web/src/lib/userDisplay.ts` (`formatUserLabel`/`formatUserName`). Where the backing
DTO only ever carried a raw username (`TaskDto.AssignedStaffUsername`/`OriginalOwnerUsername`,
`ThreadDto.AssignedStaffUsername`, `TaskForwardingRuleDto.Username`/`TargetUsername`), the
backend gained matching `FullName`/`Role` fields (`AssignedStaffFullName`/`AssignedStaffRole`,
etc.) — pure DTO + mapping-code additions, since every controller path already had the full
`User` entity loaded (`AssignedStaff`/`OriginalOwner`/`User`/`TargetUser` nav properties), so no
new EF queries or joins were required. See `CODEBASE_MAP.md`'s `TasksController`/
`ThreadsController`/`TaskForwardingRulesController` sections for the exact fields.

**Reason:** Display was inconsistent across the app — some dropdowns showed only a full name
with no role, some spots (the header) showed a raw username with role, and several task/thread
list/card views could only ever show a bare username because the DTO never carried more than
that. Zohaib asked for one consistent format across the whole project.

**Alternatives considered:** Formatting inline at each call site instead of a shared helper —
rejected for consistency and maintainability, since the inconsistency being fixed was itself
caused by every call site duplicating its own ad-hoc formatting. Leaving the raw-username-only
task/thread views alone (frontend-only fix, no backend changes) — rejected; Zohaib explicitly
asked for the full scope including those views once the backend gap was explained.

---

## 2026-07-17 — Quiet Hours Start/End now shown and edited in the viewer's local time, not raw UTC

**Decision:** `SmsTab`'s Quiet Hours Start/End fields (`notifyhub-web/src/components/settings/sms-tab.tsx`)
now convert the backend's bare UTC `"HH:mm"` (`SettingsController` GET/PATCH, compared against
`DateTime.UtcNow` in `SettingsService.IsQuietHoursNowAsync`) to/from the browser's local time at
the component boundary, via two new helpers in `notifyhub-web/src/lib/dateUtc.ts`:
`utcTimeToLocal`/`localTimeToUtc`. Both combine the bare time with *today's* date to pick up the
current UTC offset (DST-correct for now) before reading back hours/minutes. The "(UTC)" labels
and the card's "this UTC window" description were dropped since the displayed/edited value is
now local, matching every other time field in the app. Backend storage and the dispatcher's
gating logic are unchanged — this is a display/edit-time conversion only.

**Reason:** An audit of every timestamp display in the frontend (prompted by Zohaib asking
where UTC leaks onto the screen instead of local time) found Quiet Hours was the only site that
showed raw UTC to the user — every other timestamp (Inbox, Tasks, Audit Log, SMS History,
Reminders) already converts via `dateUtc.ts`'s existing full-datetime helpers or an equivalent
inline `new Date(...).toLocaleString()`, safe because the API always sends `Z`-suffixed UTC ISO
strings. Quiet Hours couldn't reuse those helpers because it has no date component at all — a
bare time-of-day needed a dedicated conversion, not just a parse.

**Alternatives considered:** Converting on the backend instead (storing/serving local time) —
rejected; the dispatcher's gate and every stored value must stay in one unambiguous reference
(UTC), same reasoning as every other timestamp in the system — converting only at the display
edge keeps that invariant intact.

**Impacted modules:** SMS Settings (`notifyhub-web/src/lib/dateUtc.ts`,
`notifyhub-web/src/components/settings/sms-tab.tsx`). No backend or schema change.

## 2026-07-17 — Recurring task's Description now carries over to the next occurrence

**Decision:** `TasksController.SpawnNextOccurrenceIfDue` now copies `Description` from the
completed task onto the newly spawned occurrence, alongside the `TaskType` it already carried
over. Previously `Description` was left unset on the new occurrence.

**Reason:** Zohaib reported that completing a recurring task appeared not to create the next
occurrence. The spawn logic itself was working (and already covered by a passing test) — the
new occurrence just silently dropped out of any Task Board/Grid view filtered by the original
task's `description` (a substring filter), since the new row's `Description` was null. This
reverses the original design call from when recurrence/description shipped (increment 1),
documented at the time as: "category carries over; Description doesn't (it was tied to the
message that prompted the completed occurrence, would go stale)." That reasoning is still valid
in the abstract, but in practice it broke discoverability of ongoing recurring series under any
description filter, which outweighs the staleness concern.

**Alternatives considered:** Leaving Description blank and instead fixing discoverability in the
UI (e.g. a banner/toast surfacing the newly spawned task) — rejected as more complex for the same
practical outcome, and the "next occurrence is basically the same task" framing (which is why
`TaskType` already carries over) supports just copying `Description` too.

**Impacted modules:** Tasks (`TasksController.SpawnNextOccurrenceIfDue`,
`TasksControllerTests.Update_CompletingRecurringTask_SpawnsNextOccurrence`); `CODEBASE_MAP.md`
§3 `TasksController` `PATCH api/tasks/{id}` row updated in the same change.

## 2026-07-17 — Template-edit safety net now eagerly re-renders queued SMS, instead of nulling for a lazy dispatch-time render

**Decision:** `TemplatesController.Update` (P9-05 dual-safety net #1) previously responded to a
template `Body` edit by nulling `RenderedBody` on every affected `Queued` `OutboundMessage`,
leaving the actual re-render to happen lazily inside `MessageDispatcher.DispatchOneAsync` at
the message's real dispatch time (net #2). Changed net #1 to eagerly re-render right there in
the same PATCH request — extracted the merge-field rendering logic (previously a private
method on `MessageDispatcher`) into a new shared `MessageBodyRenderer` service
(`NotifyHub.Infrastructure/Messaging/MessageBodyRenderer.cs`), used by both
`TemplatesController.Update` and `MessageDispatcher.DispatchOneAsync`. `ScheduledAt`/
`ExpiresAt`/`Status` are untouched by the sweep — only `RenderedBody` content changes, so a
template edit still sends at its already-scheduled time, it just reflects the new template
content immediately instead of only once the dispatcher happens to pick the message up.
`DispatchOneAsync`'s `RenderedBody is null` guard remains as a defensive backstop (net #2) for
any row that still reaches dispatch without a rendered body (e.g. a Reminder SMS created with
no committed body, rule 31).

**Reason:** Zohaib asked for pending SMS content to be regenerated as soon as a template is
edited (so it's visible/correct right away) rather than staying blank until the dispatcher's
next poll picks the message up at its scheduled time, while explicitly keeping the scheduled
send time untouched.

**Alternatives considered:** Leaving the null-and-defer behavior as-is (net #2 already
guarantees fresh content by actual send time) — rejected, since the ask was specifically for
the content to update immediately, not just correctly-eventually. A background sweep job
polling for `Queued` rows with a null `RenderedBody` — rejected as unnecessary; the edit
request itself is the trigger, so doing the re-render synchronously in that same request is
simpler and needs no new worker/poll loop.

**Impacted modules:** SMS (`docs/DOCUMENT_INDEX.md` SMS entry) — `TemplatesController.Update`,
`MessageDispatcher.DispatchOneAsync`, new `MessageBodyRenderer`. Also removed the
InMemory-vs-real-provider `ExecuteUpdateAsync` branch in `TemplatesController.Update` — a
per-row render can't be expressed as a bulk update, so both providers now share one
tracked-loop path.

## 2026-07-16 — Task creation Due Date defaults to a priority-based suggestion (wires up FR-008), not a flat "now"

**Decision:** `NewTaskForm.tsx`/`CreateTaskForm.tsx`'s Due Date field now pre-fills using the
existing `TaskDueDateDefaults.DefaultDueAt` offsets (Urgent +4h, High +1day, Medium +3days,
Low +7days from now), mirrored client-side in a new `lib/taskDueDateDefaults.ts`. It
recomputes when Priority changes, until the user edits Due Date directly — at which point
their manual value is left alone regardless of further Priority changes. `CreateTaskForm.tsx`
additionally resets Priority/Due Date to a fresh default when its dialog reopens (it stays
mounted between opens, unlike `NewTaskForm.tsx`).

**Reason:** Zohaib asked for Due Date to default to "the current date/time" instead of
starting blank and required. The backend already computes a priority-based suggestion
(`ThreadsController.CreateTask`'s `request.DueAt ?? TaskDueDateDefaults.DefaultDueAt(...)`)
and Settings → Task tab already describes this as the intended behavior ("Due dates
auto-suggested by priority when creating a task"), but P9-01d (see the entry above this one)
made Due Date a client-required field with no default at all, so that suggestion was never
actually reachable through either creation form. Asked Zohaib directly whether he wanted a
flat "now" or the priority-based version; he confirmed the latter, so this wires up the
already-built FR-008 engine on the frontend rather than introducing a second, competing
notion of "default due date."

**Alternatives considered:** A flat "now" default ignoring Priority — rejected per Zohaib's
explicit choice, and it would have left the FR-008 engine and its Settings-page description
still effectively dead code on the creation forms.

## 2026-07-16 — "Assigned to" in the Task board's New Task form must not reset when the thread changes

**Decision:** `NewTaskForm.tsx`'s `handleThreadChange` no longer clears `assignedStaffId`
when a different patient/thread is picked — it only updates `threadId`. "Assigned to" keeps
whatever it currently holds (the initial default-to-current-user, or a manual pick) across
thread changes; the user can still change it manually at any time via the existing `Select`.

**Reason:** Zohaib reported "Assigned to" correctly defaulted to the logged-in user on open,
but got cleared as soon as a patient/thread was selected. Root cause:
`handleThreadChange` explicitly reset `assignedStaffId` to `""` so `TaskAssignmentFields`'s
default-chain effect would recompute from the newly-selected thread's current owner — which
either silently restored the same default (invisible) or silently swapped to a different
owner (read as the field being blanked/changed unexpectedly). Confirmed the desired priority:
default to the creator, and never let a later thread pick override an already-set value.

**Alternatives considered:** Keeping the "thread's current owner" re-derivation but only when
the field still holds its *default* value (i.e. track whether the user manually touched it) —
rejected as unnecessary complexity; Zohaib's ask was for the field to simply stop reacting to
thread changes at all.

## 2026-07-16 — Task auto-forwarding: apply rules unconditionally, not just while the source user is Inactive

**Decision:** `FallbackUserResolver.ResolveNewTaskAssigneeAsync` now looks up a matching
`TaskForwardingRule` for the natural assignee *before* checking their Active/Inactive status.
If a currently-in-window rule exists and its target is Active, the target is used —
regardless of whether the natural assignee themselves is Active. Previously, an Active
natural assignee short-circuited the method entirely and the rule was never even queried.
Also reworded the now-inaccurate `"(natural assignee inactive)"` audit-detail string
(`ThreadsController.CreateTask`) to `"(forwarding rule applied)"`.

**Reason:** Zohaib created a forwarding rule (admin → chadmin) with admin Active, created a
task assigned to admin, and it correctly should have landed on chadmin but didn't. Traced the
cause to the Active-gate above. That gate is not a stray bug — it's `STEP9_PLAN.md` rule 1
exactly as written ("New tasks created while the original assignee is **Inactive** check the
forwarding rule first"), a locked, pre-approved spec. Confirmed directly with Zohaib that
admin was Active at the time, and that he now wants forwarding to apply whenever a rule
matches, regardless of source-user status — a deliberate reversal of rule 1, consistent with
the prior session's decision to open rule *creation* up to any From/To pair rather than only
self-service Inactive/OnLeave cover.

**Alternatives considered:** Leaving the Active-gate in place and telling Zohaib to mark
admin Inactive/OnLeave to get the rule to apply — rejected, contradicts his explicit,
confirmed expectation that the rule should apply unconditionally.

## 2026-07-16 — Show "Original provider" / "Assigned to" as two fields on a forwarded task's Detail Sheet

**Decision:** `TaskDto` gained `OriginalOwnerUsername` (backend: `TasksController`'s
`List`/`Detail`/`Update`/`Forward` all `.Include(t => t.OriginalOwner)`;
`ThreadsController.CreateTask`'s separate `ToTaskDto` fetches it alongside the existing
assignee-username lookup). `components/v2/task-detail-sheet.tsx` now renders two lines
("Originally assigned to X" / "Now assigned to Y") in place of the single "Assigned to X"
line whenever `originalOwnerId !== assignedStaffId` — i.e. only once a task has actually been
forwarded away from its original owner. The compact Task Card is intentionally left showing
only the current assignee.

**Reason:** Zohaib recalled asking for this during Step 9 planning. `STEP9_PLAN.md` rule 11
already covers the *data* side ("Store both Original Assignee and Current Assignee — no new
columns needed, existing schema covers it") — `TaskItem.OriginalOwnerId`/`AssignedStaffId`
both already existed and already behaved correctly (a re-forward only ever touches
`AssignedStaffId`), but no DTO ever carried the original owner's *username* and no UI ever
surfaced it. This closes that gap; it doesn't change any assignment/forwarding behavior.

**Alternatives considered:** Also adding a second line / badge to the compact Task Card —
Zohaib asked for Detail Sheet only, to keep the card compact.

---

## 2026-07-16 — Task forwarding: add an explicit "From" user field, remove self-service scoping

**Decision:** `TaskForwardingRulesController`'s "From" side is no longer implicit/hardcoded to
the caller. `TaskForwardingRuleRequest`/`Dto` gained `UserId`/`Username`, and any authenticated
user can now create, edit, or delete a forwarding rule for **any** From/To user pair — not just
their own. The `List`/`Update`/`Delete` ownership checks (`rule.UserId != callerId`) were
removed; `GET api/task-forwarding-rules` now returns every rule org-wide. Validation still
rejects `userId == targetUserId` (same user can't appear on both sides of one rule) and still
requires the target to be `Status == Active`; the overlap check (rules 4/9) still applies
per-From-user, just keyed off the request's `userId` instead of the caller's. Frontend
(`components/settings/task-tab.tsx`) gained a "Forward from" picker next to "Forward to," with
each list excluding whichever user is selected in the other field.

**Reason:** Zohaib asked for an explicit From field with same-user cross-exclusion between From
and To. This directly reverses a scope decision flagged as an inference in `CODEBASE_MAP.md`
when the feature was first built (P9-10): "self-service scoping... is an inference... flagged as
a reasonable reading, not an explicit requirement either way." Confirmed via clarifying question
that the intent is to open the feature to everyone, not just gate the new field behind Admin.

**Alternatives considered:** Admin-only management of other users' rules (regular users keep
today's self-service view) — rejected, not what was asked for. Adding the From field but keeping
it locked to the current user (cosmetic-only change) — rejected, defeats the purpose of a "From"
picker.

## 2026-07-15 — Reminder SMS Event Time substitution: extend `PreviewTemplate` with an `isReminder` flag, not a new endpoint

**Decision:** Fixed `{{appointment_time}}` never being replaced by the picked Event Time in the
Reminder SMS dialog by adding an optional, additive `isReminder` query parameter to the existing
shared `GET /api/threads/{id}/templates/{templateId}/preview` endpoint, rather than introducing
a separate reminder-specific preview endpoint. When `true`, it skips the real-`Appointment`
lookup and leaves `{{appointment_time}}` as a literal unresolved token, which the frontend then
substitutes with the staff member's own picked Event Time as they select/change it. Also added an
`EventTime`-based fallback branch to `MessageDispatcher.RenderAsync` for the same merge field, to
close the same gap on the blank-body dispatch-time path.

**Reason:** The Reminder SMS dialog was reusing the same preview endpoint as the Standard SMS
composer, whose `{{appointment_time}}` resolution is hardcoded to the patient's real
`Appointments` table — correct for Standard SMS, but wrong for Reminder SMS, which is
deliberately Appointment-independent (`STEP9_PLAN.md` rule 34). The two modes share everything
except that one resolution branch, so an additive parameter is the smaller, safer diff and is
provably non-breaking for the Standard composer (which never passes it).

**Alternatives considered:** A separate `reminder`-specific preview endpoint — rejected as
unnecessary duplication of the patient-lookup/template-lookup/404 boilerplate for one `if`
branch. Introducing a new `{{event_time}}` merge-field token — rejected to stay backward-
compatible with already-seeded templates/bookmarks that use `{{appointment_time}}`.

**Impacted modules:** SMS (`docs/DOCUMENT_INDEX.md` SMS entry) — `ThreadsController.
PreviewTemplate`, `MessageDispatcher.RenderAsync`, `reminder-sms-dialog.tsx`. Follow-up flagged,
not fixed here: `MessagesController.UpdateReminder` still doesn't re-render `RenderedBody` when
an existing `Queued` reminder's `EventTime` is edited (see `STATUS.md`'s known-limitations list).

## Template

Copy this block for each new entry, fill it in, and replace the date/title.

```
## YYYY-MM-DD — <decision title>
**Decision:** <what was decided>
**Reason:** <why — the constraint, tradeoff, or requirement that drove it>
**Alternatives considered:** <what else was on the table, and why it was rejected>
**Impacted modules:** <features/files/docs affected — cross-reference docs/DOCUMENT_INDEX.md entries where relevant>
```
