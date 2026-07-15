# Tasks — Worker (Escalation)

Anchor file for the Tasks feature's background-job documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §4, `EscalationWorker` (config-driven poll, `Escalation:PollIntervalSeconds`)
  and `EscalationJob.EscalateOverdueTasksAsync` (overdue batch, fallback-Admin reassignment,
  audit trail).
- `CODEBASE_MAP.md` §4, P9-12 note: the same poll cycle also runs `RevertExpiredLeaveAsync`
  (flips `OnLeave` users back to `Active` once `LeaveTo` passes) — unrelated to escalation,
  co-located for the free poll loop rather than a new worker process.
- `PROJECT_CONTEXT.md` BR-004 for the escalation/reassignment business rule.

**Note**: `docs/DOCUMENT_INDEX.md`'s Tasks entry originally modeled this as
`Worker/Dispatcher.md` per the requested example format. Corrected against the actual code —
`MessageDispatcher`/`DispatcherWorker` is the SMS-sending worker (see `docs/Worker/Dispatcher.md`
and `CODEBASE_MAP.md` §4/§4b), not a Tasks worker. Filed here instead, per this repo's own
"if docs conflict with code, trust the code" rule.

Update this file only when Tasks-worker documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
