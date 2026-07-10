# NotifyHub — Build Status

## Current step
Step 1 of 6 — Solution skeleton, docker-compose, CI, JWT auth end-to-end (in progress)

## Step 1 checklist
- [ ] Solution skeleton (5 .NET projects + notifyhub-web) with correct project references
- [ ] users/refresh_tokens EF Core entities + InitialCreate migration
- [ ] Auto-migrate on API startup
- [ ] Idempotent seed step: Admin + Staff users
- [ ] JWT login + refresh endpoints + RBAC wired server-side
- [ ] React login screen (apiClient, AuthContext, ProtectedRoute, toast on error)
- [ ] docker-compose: mysql healthcheck + depends_on, api/worker/web wired
- [ ] GitHub Actions CI: dotnet build+test, npm build, green on push
- [ ] Reviewed by Zohaib

## What's implemented
(updated as steps complete)

## What's left
- FR-001 to FR-019: not started (steps 2-6)

## How to run
```
docker-compose up
```
Requires a `.env` file at the repo root — copy `.env.example` and fill in values (see `.env.example` for the full list of required keys).

Seeded accounts (values from your local `.env`, not committed):
- Admin: `SEED__ADMINUSERNAME` / `SEED__ADMINPASSWORD`
- Staff: `SEED__STAFFUSERNAME` / `SEED__STAFFPASSWORD`

## Documented deviations from PROJECT_CONTEXT.md
- **Refresh endpoint is its own route** (`POST /api/auth/refresh`) rather than an overload of `POST /api/auth/login`. §6a's text literally says "refresh via `/api/auth/login`," but §8's endpoint table doesn't list a refresh route and §11a's separate 30min/7-day access/refresh expiry design only makes sense if refresh doesn't require re-submitting a password. Resolved with Zohaib during planning — dedicated `/api/auth/refresh` endpoint chosen.
- **RFC 7807 ProblemDetails enabled in step 1**, not deferred to step 6 as §11 implies. .NET 8's `AddProblemDetails()` + automatic `ValidationProblemDetails` are effectively free to turn on now and avoid a retrofit; full custom business-exception-to-ProblemDetails mapping remains a step-6 item.

## Known limitations (by design, not bugs)
- Worker's step-1 logic is a placeholder heartbeat only, proving DB connectivity — dispatcher/scheduler/escalation land in step 2+.
- No user-management UI (users seeded via script only, per §4).
- Refresh token is kept in-memory on the frontend only (no localStorage, per locked §6a architecture) — a hard page reload forces re-login even within the 7-day refresh window. Accepted tradeoff of the locked architecture, not a bug.
- Worker does not gate its startup on the API's migration completing (no `api` healthcheck endpoint exists to gate on); it retries DB connections defensively instead.
- Web container runs the Vite dev server, not a production nginx build — chosen for 3-day-scope simplicity.
- CI's step-1 integration test runs against EF Core InMemory, not a real MySQL service container — a real MySQL container will likely be added to CI in step 2 for the dispatch-pipeline integration test (idempotency/retry needs real unique-constraint behavior).
- §6d's two intentionally-not-built items (multi-worker dispatcher locking, client-side template char counter) remain out of scope — not relevant yet in step 1, noted here so they aren't lost track of.

## Open questions
None currently blocking.

## Change log
| Date | Step | Summary |
|---|---|---|
| 2026-07-11 | 1 | Repo initialized, planning complete, implementation started |
