# SMS — Worker (Dispatcher)

Anchor file for the SMS feature's background-job documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §4, `DispatcherWorker` (fixed 5s poll) and `MessageDispatcher.
  DispatchDueMessagesAsync` (Quiet Hours gate, `ScheduledAt` gating, expiry sweep, per-message
  dispatch/retry/backoff).
- `CODEBASE_MAP.md` §4b — this same worker/dispatcher handles Reminder SMS, not a separate
  process (rule 22: no parallel send path). `ReminderWorker`/`ReminderScheduler` were retired
  entirely in P9-08.
- `PROJECT_CONTEXT.md` FR-001–004, FR-009, BR-002, BR-003, BR-011 for the functional spec and
  business rules.

**Note**: `docs/DOCUMENT_INDEX.md`'s SMS entry originally modeled this as `Worker/Reminder.md`
per the requested example format. Corrected against the actual code — there is no dedicated
Reminder worker; it was deleted in P9-08 and replaced by this generic engine running through
the same `MessageDispatcher` as Standard SMS. Filed here instead, per this repo's own "if docs
conflict with code, trust the code" rule.

Update this file only when SMS-worker documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
