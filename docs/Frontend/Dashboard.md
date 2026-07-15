# Dashboard — Frontend

Anchor file for the Dashboard feature's frontend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §6a — `src/pages/DashboardPage.tsx` (unversioned, no legacy equivalent).
  Stat cards (my open/escalated/overdue tasks, unread-thread count), Admin-only org-wide
  task-counts card, quick links, recent-activity list (reuses `AUDIT_ACTION_CONFIG`/
  `StatusBadge` from the Audit Log screen) — all sourced from one `useDashboardSummary()` call,
  no screen-specific aggregation on the frontend.
- `CODEBASE_MAP.md` §6a — `components/v2/task-nav-widget.tsx`'s `TaskNavWidget` (mounted in
  `AppShell.tsx`'s header): popover badge count of the caller's non-terminal assigned tasks,
  navigates via `TaskBoardPageV2`'s existing `?task={id}` deep-link mechanism.
  `hooks/useDashboard.ts`: `useDashboardSummary()`.
- `CODEBASE_MAP.md` §6, routing note — `/` renders `DashboardPage` directly since increment 13
  (no longer redirects to `/inbox`); `docs/Frontend/AuditLog.md`/`docs/Frontend/Tasks.md` for
  the components it reuses.
- `PROJECT_CONTEXT.md` — not a numbered FR; added as a post-login landing page in increment 13,
  purely a read-side aggregation over existing Tasks/Inbox/Audit data.

Update this file only when Dashboard-frontend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
