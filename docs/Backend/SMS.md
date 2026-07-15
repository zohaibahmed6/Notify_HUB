# SMS — Backend

Anchor file for the SMS feature's backend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §3, `MessagesController` (cancel/update reminder, SMS History report),
  `ThreadsController` (`Reply`, `CreateReminder`), `WebhooksController`/`MockGatewayController`
  (delivery receipts, mock gateway).
- `CODEBASE_MAP.md` §4b, the full Reminder SMS engine writeup (schema, settings, pure
  calculations in `ReminderScheduleCalculator`).
- `CODEBASE_MAP.md` §5 for idempotent dispatch/backoff/opt-out logic shared by Standard and
  Reminder SMS (rule 22: no parallel send path).
- `PROJECT_CONTEXT.md` FR-001–004, FR-009, BR-001–003, BR-009–011, BR-013 for the functional
  spec and business rules; `STEP9_PLAN.md` for the P9-08 Reminder SMS rule numbering
  (rules 3/5–10/15–18/27–32/34).

Update this file only when SMS-backend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
