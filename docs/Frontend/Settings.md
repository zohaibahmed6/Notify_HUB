# Settings — Frontend

Anchor file for the Settings feature's frontend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. This covers the shared `SettingsPage.tsx` shell only — each of its 7
tabs is a thin wrapper whose real logic belongs to the feature it configures (see the
per-tab pointers below). No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §6a — `pages/SettingsPage.tsx` (unversioned, shared by both UI modes), 7
  shadcn `Tabs`: General (`general-tab.tsx`, thin read-only), SMS (`sms-tab.tsx` — Quiet Hours/
  rate-limit/reminder-defaults forms, see `docs/Backend/SMS.md`), Task (`task-tab.tsx` —
  read-only due-date defaults + P9-10 Task-forwarding self-service card, see
  `docs/Backend/Tasks.md`), Template (`template-tab.tsx` — Bookmark CRUD, see
  `docs/Frontend/Templates.md`), Notification (`notification-tab.tsx`, client-only, no
  backend), User Management (`user-management-tab.tsx`, see `docs/Frontend/Users.md`), System
  (`system-tab.tsx` — read-only diagnostics).
- `CODEBASE_MAP.md` §6a — the legacy/redesign manual-toggle UI was removed from Settings as
  part of the 7-tab rebuild (increment 11); `UIVersionContext`/`VersionedRoute` themselves are
  untouched.
- `PROJECT_CONTEXT.md` — Quiet Hours and per-patient rate limiting were originally
  "explicitly dropped" stretch goals, later built anyway in increment 10 (see `STATUS.md`).

Update this file only when Settings-shell documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
