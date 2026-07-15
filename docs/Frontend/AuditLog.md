# Audit Log — Frontend

Anchor file for the Audit Log feature's frontend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §6, legacy `AuditLogPage.tsx` — role-branches on `user.role` (Admin gets an
  actor filter + `/api/audit`, Staff gets `/api/audit/mine`); action/date-range filters,
  paginated table.
- `CODEBASE_MAP.md` §6a, `AuditLogPageV2.tsx` — GitHub/Datadog-style table, client-side column
  sort (current page only), `StatusBadge`/`AUDIT_ACTION_CONFIG` per action type, a day-by-day
  `Sparkline` (current page/filter only, not a full-history aggregate). **Product decision**:
  the redesign is Admin-only — Staff's `/api/audit/mine` view is intentionally dropped in the
  new UI, enforced both in `AppShell.tsx`'s nav-link filter and the page's own render guard.
  Legacy is unaffected.
- `CODEBASE_MAP.md` §6a — shared `src/lib/dateRangeFilter.ts` (local-day date-range util, bug
  fix for a UTC/local-day mismatch) is used by both this screen and the Tasks filter bar.
- `PROJECT_CONTEXT.md` FR-011 for the functional spec.

Update this file only when Audit-Log-frontend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
