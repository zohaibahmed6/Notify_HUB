# NotifyHub — Build Status

## Current step
Step 1 of 6 — Solution skeleton, docker-compose, CI, JWT auth end-to-end — **code complete, pending your review** (see "Needs your verification" below before moving to step 2).

## Step 1 checklist
- [x] Solution skeleton (5 .NET projects + notifyhub-web) with correct project references
- [x] users/refresh_tokens EF Core entities + InitialCreate migration
- [x] Auto-migrate on API startup (with InMemory/EnsureCreated fallback for tests)
- [x] Idempotent seed step: Admin + Staff users
- [x] JWT login + refresh endpoints + RBAC wired server-side
- [x] React login screen (apiClient, AuthContext, ProtectedRoute, toast on error)
- [x] docker-compose: mysql healthcheck + depends_on, api/worker/web wired
- [x] GitHub Actions CI: dotnet build+test, npm build, on push
- [ ] Reviewed by Zohaib

## What's implemented
- **Solution skeleton**: `NotifyHub.sln` with Api/Worker/Domain/Infrastructure + Domain.Tests/Integration.Tests, referenced per §2. `notifyhub-web` scaffolded (Vite + React + TS + Tailwind + shadcn-style components).
- **Domain**: `User`, `RefreshToken` entities, `UserRole` enum, `PasswordPolicy` (real, unit-tested validation logic for §7's password rule).
- **Infrastructure**: `NotifyHubDbContext` (Pomelo MySQL provider), EF Core configs (BIGINT PKs, unique indexes on `username`/`token_hash`, enum-as-string convention), `InitialCreate` migration committed. `IDbSeedStep`/`DbSeedRunner`/`UserSeedStep` — idempotent (skips if any user exists), reads credentials from config, validates them against `PasswordPolicy` and fails fast if misconfigured.
- **Api**: DbContext registered, auto-migrates on startup (`Database.Migrate()` for MySQL, `EnsureCreated()` for the InMemory test provider — branches on `IsRelational()`), Swagger, CORS locked to `Cors:WebOrigin`, RFC 7807 ProblemDetails enabled. JWT auth: `POST /api/auth/login`, `POST /api/auth/refresh` (dedicated route — see deviation below), `GET /api/auth/me`, `GET /api/auth/admin-only` (RBAC proof route). Access tokens: HMAC-SHA256, 30min expiry, role claim. Refresh tokens: opaque, SHA-256-hashed at rest, rotated on use (old row revoked + linked via `ReplacedByTokenId`) so replay is detectable and rejected.
- **Worker**: DbContext registered; `PlaceholderHeartbeatWorker` proves DB connectivity with retry/backoff (real dispatcher/scheduler/escalation land in step 2+).
- **Tests**: 10 Domain unit tests (`PasswordPolicyTests`) + 10 integration tests (`AuthEndpointTests`, via `WebApplicationFactory` + EF InMemory) — login happy-path/bad-creds, refresh rotation + replay rejection, RBAC (401 unauthenticated, 403 wrong role, 200 correct role). **All 20 passing.**
- **Docker**: Dockerfiles for api/worker/web, `docker-compose.yml` with mysql healthcheck + `depends_on: condition: service_healthy` for api/worker, ports per §11a (api:5000, web:5173, mysql:3306).
- **CI**: `.github/workflows/ci.yml` — dotnet build+test with coverage collection, npm install + build, triggers on push.
- **React**: apiClient (fetch wrapper, JWT header, silent-refresh-then-logout-event on 401), in-memory `tokenStore`, `AuthContext`, `ProtectedRoute`, `LoginPage` (required-field validation, error toast per §6c), placeholder `HomePage` (real screens land in steps 3–5).

## What's left
- FR-001 to FR-011: not started (steps 2–5: seed data, outbound pipeline, inbound/tasks, reminders/audit/50k seed)
- FR-016 (3 ADRs), FR-018 (OWASP self-assessment), FR-019 (AI usage log): step 6
- FR-012 (incremental commits), FR-013 (≥70% Domain coverage, integration test), FR-014 (CI), FR-015 (one-command run), FR-017 (Swagger): in place structurally for step 1's scope, will keep extending through remaining steps

## Needs your verification (could not run in this build environment)
This sandbox has **no Docker and no Node/npm installed**, so two things are written but not executable-tested here:
1. **`docker-compose up`** — please run this yourself and confirm: all 4 containers start, `mysql` reports healthy before `api`/`worker` start, `api` auto-migrates + seeds cleanly on a fresh volume, and again cleanly on a restart (no duplicate seed rows — this is the idempotency check for FR-003-adjacent seed logic). Then `curl -X POST http://localhost:5000/api/auth/login` with your `.env` admin credentials should return a token pair.
2. **React app** — no `package-lock.json` exists yet (couldn't run `npm install` here). Please run `npm install` in `notifyhub-web/` once, commit the generated lockfile, and switch CI's `npm install` back to `npm ci` (with caching restored) for reproducible builds — flagged inline in `ci.yml`. Then `npm run dev` and confirm login works end-to-end in the browser against the dockerized API, and that a bad-password attempt shows the toast per §6c.
3. **CI green run** — no GitHub remote is configured yet, so the Actions workflow has never actually run. Push once you're ready and confirm it goes green.

Everything else (backend build/test, migration SQL shape, JWT/RBAC logic, seed idempotency logic) was verified directly: `dotnet build`/`dotnet test` both green, 20/20 tests passing, migration reviewed by hand.

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
- **CI uses `npm install` instead of `npm ci`** for now, purely because no `package-lock.json` exists yet (environment constraint, not a spec decision) — see "Needs your verification" above.
- **Two extra diagnostic routes** (`GET /api/auth/me`, `GET /api/auth/admin-only`) added beyond §8's table, specifically to prove RBAC/JWT wiring end-to-end in step 1 (no other protected route exists yet to test against). `/me` is a reasonable "who am I" endpoint the frontend will likely want anyway; `/admin-only` exists purely as an RBAC test fixture.

## Known limitations (by design, not bugs)
- Worker's step-1 logic is a placeholder heartbeat only, proving DB connectivity — dispatcher/scheduler/escalation land in step 2+.
- No user-management UI (users seeded via script only, per §4).
- Refresh token is kept in-memory on the frontend only (no localStorage, per locked §6a architecture) — a hard page reload forces re-login even within the 7-day refresh window. Accepted tradeoff of the locked architecture, not a bug.
- Worker does not gate its startup on the API's migration completing (no `api` healthcheck endpoint exists to gate on); it retries DB connections defensively instead.
- Web container runs the Vite dev server, not a production nginx build — chosen for 3-day-scope simplicity.
- CI's integration test runs against EF Core InMemory, not a real MySQL service container — a real MySQL container will likely be added to CI in step 2 for the dispatch-pipeline integration test (idempotency/retry needs real unique-constraint behavior).
- Post-login redirect goes to a placeholder `HomePage`, not a role-specific screen — the 5 real screens (§6b) don't exist until steps 3–5.
- §6d's two intentionally-not-built items (multi-worker dispatcher locking, client-side template char counter) remain out of scope — not relevant yet in step 1, noted here so they aren't lost track of.

## Open questions
None currently blocking. (One ambiguity — the refresh endpoint shape — was raised and resolved with Zohaib before implementation; see deviations above.)

## Change log
| Date | Step | Summary |
|---|---|---|
| 2026-07-11 | 1 | Solution skeleton, EF Core + MySQL, JWT auth (login/refresh/RBAC), seed scaffolding, Docker/compose, CI, React login screen. 20/20 tests passing. Docker-compose smoke test and npm build/browser verification pending user review (no Docker/Node in build environment). |
