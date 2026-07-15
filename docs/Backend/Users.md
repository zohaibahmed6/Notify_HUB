# Users — Backend

Anchor file for the Users/User-Management feature's backend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §3, `UsersController` — list/assignable/create/status-PATCH. Status-PATCH
  auto-forwards a deactivated user's non-terminal tasks to a fallback Active Admin in the same
  `SaveChangesAsync`; P9-12 requires `LeaveFrom`/`LeaveTo` together when transitioning to
  `OnLeave`.
- `CODEBASE_MAP.md` §3, `FallbackUserResolver` (`NotifyHub.Infrastructure/Users/
  FallbackUserResolver.cs`) — shared fallback-Admin lookup used by both `UsersController`'s
  status-PATCH and `EscalationJob`; `ResolveNewTaskAssigneeAsync` (P9-10, forwarding-rule-aware)
  is separate and only called from `ThreadsController.CreateTask` — see `docs/Backend/Tasks.md`.
- `CODEBASE_MAP.md` §3, `ActiveUserRequiredFilter` (`NotifyHub.Api/Users/
  ActiveUserRequiredFilter.cs`) — global read-only enforcement for non-Active users, plus
  assignment-target validation in `ThreadsController.Assign`/`TasksController.Update`.
- `CODEBASE_MAP.md` §4, P9-12: `EscalationWorker`'s `RevertExpiredLeaveAsync` flips `OnLeave`
  users back to `Active` once `LeaveTo` passes (piggybacked on the escalation poll loop, not a
  separate worker — see `docs/Worker/Escalation.md`).
- `PROJECT_CONTEXT.md` §4 (roles/permissions), `docs/adr/0003-rbac-model.md` (two-role
  Admin/Staff model, rejected alternatives).

Update this file only when Users-backend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
