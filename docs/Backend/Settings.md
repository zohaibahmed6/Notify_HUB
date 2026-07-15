# Settings — Backend

Anchor file for the Settings feature's backend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §3, `SettingsController` — `GET`/`PATCH api/settings` (Quiet Hours,
  rate-limit, reminder defaults; PATCH is Admin-only, validates `HH:mm` strings and positive
  counts), `GET api/settings/system-info` (live diagnostics, not `SystemSetting`-backed).
- `CODEBASE_MAP.md` §3, `SettingsService` (`NotifyHub.Infrastructure/Settings/
  SettingsService.cs`) — typed accessors over the generic `SystemSetting` key-value table;
  Quiet Hours and rate limiting both default disabled.
- `CODEBASE_MAP.md` §4b — Reminder SMS defaults (`ReminderOffsetMinutesKey`/
  `ReminderExpiryOffsetMinutesKey`) are also `SystemSetting`-backed, exposed through this same
  controller.
- `CODEBASE_MAP.md` §5, `QuietHoursCalculator`/`RateLimitChecker` — the pure logic these
  settings feed into (dispatch gating and per-patient send limits, see `docs/Worker/
  Dispatcher.md` / `docs/Backend/SMS.md`).
- `PROJECT_CONTEXT.md` — Quiet Hours/rate limiting were originally out-of-scope stretch goals
  (§3 "Explicitly dropped"), built anyway in increment 10 — see `STATUS.md` for that decision.

Update this file only when Settings-backend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
