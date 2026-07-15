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
