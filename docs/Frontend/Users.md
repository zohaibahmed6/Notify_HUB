# Users — Frontend

Anchor file for the Users/User-Management feature's frontend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §6a, `SettingsPage.tsx`'s "User Management" tab
  (`components/settings/user-management-tab.tsx`, backed by `hooks/useUsers.ts`) — user table,
  per-row status `Select`, create-user form.
- `CODEBASE_MAP.md` §6a, P9-12: picking `OnLeave` opens a `Dialog` collecting `LeaveFrom`/
  `LeaveTo` (both required) before submitting; the table shows the leave window for any
  currently-`OnLeave` row.
- `CODEBASE_MAP.md` §6, `useUsers.ts`: `useAssignableUsers()` (the roster every assignee-picker
  in the app uses), `useUsers(filters)`, `useCreateUserMutation()`,
  `useUpdateUserStatusMutation()` (invalidates both `["users"]` and `["tasks"]` since a status
  change can silently auto-forward tasks).
- `docs/Frontend/Tasks.md` — the Task-forwarding-rule self-service UI (Settings → Task tab) is
  documented as part of Tasks, not here, since it's caller-scoped (each user manages their own
  rules) rather than an admin User-Management concern.
- `PROJECT_CONTEXT.md` §4 (roles/permissions table) for the functional spec.

Update this file only when Users-frontend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
