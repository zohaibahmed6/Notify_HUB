# Dashboard — Backend

Anchor file for the Dashboard feature's backend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §3, `DashboardController` — `GET api/dashboard/summary`, pure read-side
  aggregation, no new business logic. `MyTasks` (`TaskCountsDto`: Open/InProgress/Escalated/
  Overdue) always scoped to the caller; `OrgTasks` (same shape, org-wide) is `null` for
  non-Admins; `UnreadThreadCount` = count of threads with `UnreadCount > 0`; `RecentActivity`
  = last 10 `AuditLogDto` rows, scoped to the caller's own actions for Staff (mirrors
  `AuditController`'s Admin/Staff split — see `docs/Backend/AuditLog.md`).
- No dedicated database entities — reads from `TaskItem`, `ConversationThread`, `AuditLog`
  (see `docs/Backend/Tasks.md`, `docs/Backend/Inbox.md`, `docs/Backend/AuditLog.md`).
- `PROJECT_CONTEXT.md` — not a numbered FR; added in increment 13 as a post-login summary.

Update this file only when Dashboard-backend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
