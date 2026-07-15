# Inbox — Backend

Anchor file for the Inbox feature's backend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §3, `ThreadsController` (list/detail/reply/assign/create-conversation) and
  `WebhooksController` (inbound routing to a patient thread, opt-out keyword matching).
- `CODEBASE_MAP.md` §5 for the pure business-rule logic (opt-out matching, thread
  find-or-create race safety via the `PatientId` unique index).
- `PROJECT_CONTEXT.md` FR-005/FR-006/FR-007, BR-001, BR-012 for the functional spec and
  business rules.

Update this file only when Inbox-backend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
