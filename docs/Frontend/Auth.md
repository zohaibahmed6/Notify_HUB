# Auth — Frontend

Anchor file for the Auth feature's frontend documentation, referenced from
`docs/DOCUMENT_INDEX.md`. No implementation detail is duplicated here — see:

- `CODEBASE_MAP.md` §6, `pages/LoginPage.tsx` (legacy) and §6a `LoginPageV2.tsx` (redesign,
  presentation-only — same validation/auth flow as legacy).
- `CODEBASE_MAP.md` §6, `context/AuthContext.tsx` (silent refresh-on-mount, listens for
  `"auth:logout"` window event), `lib/tokenStore.ts` (in-memory access-token singleton),
  `routes/ProtectedRoute.tsx` (renders `null` while bootstrapping, redirects unauthenticated to
  `/login`).
- `CODEBASE_MAP.md` §6, `lib/apiClient.ts` — JWT attached per-request; 401 handling de-dupes
  concurrent refreshes via a shared `refreshPromise`, retries once, else clears the token store
  and dispatches `"auth:logout"`.
- `STATUS.md` — flagged pre-existing e2e staleness: `loginViaUi`
  (`e2e/helpers.ts:79`) still waits for `**/inbox`, but login now navigates to `/` which
  renders `DashboardPage` (increment 13) — not something Auth itself changed, just a stale
  test helper.
- `PROJECT_CONTEXT.md` §4 (roles/permissions) for the functional spec.

Update this file only when Auth-frontend documentation needs to say something
`CODEBASE_MAP.md` doesn't already cover — otherwise just keep the cross-reference current.
