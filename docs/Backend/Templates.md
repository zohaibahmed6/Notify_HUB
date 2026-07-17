# Templates — Backend

Anchor file for the Templates + Bookmarks feature's backend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §3, `TemplatesController` — list (`isActive` filter)/create (defaults
  `IsActive=true`)/update. P9-05: editing a template's `Body` eagerly re-renders `RenderedBody`
  on every `Queued` `OutboundMessage` with a matching `TemplateId`, right away in the same
  request, using the shared `MessageBodyRenderer` (dual-safety net #1; `MessageDispatcher`'s
  unconditional re-render on dispatch remains a defensive backstop, net #2 — see
  `docs/Worker/Dispatcher.md`). `ScheduledAt`/`ExpiresAt` are untouched, so send timing is
  unaffected — only content is refreshed, and immediately rather than at next dispatch.
- `CODEBASE_MAP.md` §3, `BookmarksController` — simple Admin-only CRUD, no pagination.
- `CODEBASE_MAP.md` §3, `ThreadsController.PreviewTemplate` (P9-04) — resolves a template's
  merge fields against a thread's real patient/appointment via
  `NotifyHub.Domain.Messaging.TemplateRenderer.Render`, reused unmodified from dispatch-time
  rendering.
- `CODEBASE_MAP.md` §5, `TemplateRenderer` — the only two fields actually resolved at send time
  are `patient_name` and `appointment_time` (`AppointmentReminder` sends only); unknown fields
  are left as-is, both at preview and dispatch time (BR-013).
- `PROJECT_CONTEXT.md` FR-001, BR-013 for the functional spec and business rules.

Update this file only when Templates-backend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
