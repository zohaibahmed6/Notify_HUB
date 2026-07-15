# Tasks — Backend

Anchor file for the Tasks feature's backend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §3, `TasksController`, `ThreadsController.CreateTask`, and
  `TaskForwardingRulesController` (P9-10).
- `CODEBASE_MAP.md` §4, `EscalationJob`/`EscalationWorker` for overdue-task auto-escalation and
  reassignment (BR-004), plus P9-12's leave-revert piggybacked on the same poll loop.
- `CODEBASE_MAP.md` §5 for recurrence logic (BR-007: due-date-anchored, completion-only,
  original-owner reassignment).
- `PROJECT_CONTEXT.md` FR-008, BR-004, BR-007, BR-014 for the functional spec and business
  rules.

Update this file only when Tasks-backend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
