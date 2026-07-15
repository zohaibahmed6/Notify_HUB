# Auth — Backend

Anchor file for the Auth feature's backend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §3, `AuthController` — `POST api/auth/login`/`refresh`/`logout`
  (`[AllowAnonymous]`), `GET api/auth/me` (default authenticated), `GET api/auth/admin-only`
  (`[Authorize(Roles = "Admin")]`, a role-check smoke-test endpoint). Refresh-token cookie
  `notifyhub_refresh` (httpOnly); issuance in `IssueTokensAsync`.
- `CODEBASE_MAP.md` §3 — global `AuthorizeFilter` (every endpoint requires authentication
  unless `[AllowAnonymous]`, no role-based fallback — any authenticated user passes unless a
  controller/action opts into something stricter) and `ActiveUserRequiredFilter` (checks live
  `User.Status` from the DB, not JWT claims, so a just-deactivated user is blocked mid-token-
  lifetime — see `docs/Backend/Users.md`).
- `CODEBASE_MAP.md` §3, SignalR auth — JWT via `?access_token=` query string for `/hubs/*` only
  (browsers can't set WS headers).
- `RefreshToken` entity (`CODEBASE_MAP.md` §2) — unique index on `TokenHash`, index on
  `UserId`.
- `docs/SECURITY.md` — sub-criterion (a), authN/RBAC enforced server-side.
- `docs/adr/0003-rbac-model.md` — two-role (Admin/Staff) decision and rejected alternatives.
- `PROJECT_CONTEXT.md` §4 (roles/permissions), FR-018(a), BR-005 for the functional spec and
  business rules.

Update this file only when Auth-backend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
