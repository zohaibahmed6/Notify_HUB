# Audit Log — Backend

Anchor file for the Audit Log feature's backend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §3, `AuditController` — `GET api/audit` (`[Authorize(Roles="Admin")]`, the
  first non-default/non-webhook auth policy in the codebase), `GET api/audit/mine` (default
  authenticated, server hardcodes `actor` to the caller regardless of client input). Shared
  query logic in `QueryAsync`.
- `AuditLog` entity (`CODEBASE_MAP.md` §2) — polymorphic reference (`EntityType`/`EntityId`,
  no FK), composite index `(EntityType, EntityId)`, index on `Actor`.
- Action types written across the codebase (not centrally enumerated in one file — collected
  here since this is the one place they all need to be visible): `send`, `receipt`, `opt-out`,
  `assignment`, `escalation`, `blocked`, `superseded` (original FR-011 set, see
  `components/v2/status-config.ts`'s `AUDIT_ACTION_CONFIG`); plus `forward` (manual + auto
  task forwarding), `status-change` (P9-12 leave revert, User status changes), `expired`
  (P9-07 message expiry), `reminder-created`/`reminder-updated`/`reminder-cancelled` (P9-08).
- `PROJECT_CONTEXT.md` FR-011 — the 5 explicit required event types (send, delivery receipt,
  opt-out, thread assignment, task escalation) are a subset of the actual action types above.

Update this file only when Audit-Log-backend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
