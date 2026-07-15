# NotifyHub

Multi-channel patient messaging platform: templated outbound SMS pipeline, a two-way patient
inbox, and task orchestration — built as a solo 3-day assessment build. Full requirements/decision
record lives in [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md); this README is the practical
"how do I run/test/understand this" entry point.

New and non-technical? Start with [`docs/HOW_IT_WORKS.md`](docs/HOW_IT_WORKS.md) — a plain-English
walkthrough with a flow diagram, no jargon.

## Stack

ASP.NET Core Web API + EF Core (MySQL) + SignalR + JWT auth, a separate `Worker` process for
background jobs, React 18 + Vite + TypeScript + TanStack Query + shadcn/ui on the frontend. See
`PROJECT_CONTEXT.md` §2 for the full table and rationale, and [`docs/adr/`](docs/adr/) for the
three architecture decisions that needed rejected-alternatives writeups.

```
NotifyHub.sln
├── NotifyHub.Api/            REST endpoints, SignalR hub, Swagger, auth
├── NotifyHub.Worker/         BackgroundServices: dispatcher, escalation job (the old appointment-polling reminder scheduler was retired in Step 9 — reminders are now event-based, see CODEBASE_MAP.md §4b)
├── NotifyHub.Domain/         Entities, business-rule logic (no EF/HTTP deps)
├── NotifyHub.Infrastructure/ EF Core, MySQL, mock SMS gateway, seed data
├── NotifyHub.Tests/          xUnit: Domain.Tests, Integration.Tests
└── notifyhub-web/            React app (+ notifyhub-web/e2e/ Playwright suite)
```

## Run it (one command)

```
cp .env.example .env   # fill in values — see comments in the file
docker-compose up
```

This boots MySQL, the Api (auto-applies EF Core migrations on startup), the Worker, and the web
frontend, and seeds demo data (patients/appointments/templates/messages + a ~50,000-message
performance seed spread across ~1,000 synthetic threads, FR-010) — idempotent, so restarting the
stack doesn't duplicate anything.

- Web app: http://localhost:5173
- API + Swagger: http://localhost:5000/swagger
- Seeded accounts: `SEED__ADMINUSERNAME`/`SEED__ADMINPASSWORD` (Admin),
  `SEED__STAFFUSERNAME`/`SEED__STAFFPASSWORD` (Staff), plus an optional second Staff account —
  values come from your own `.env`, never committed.

## Screens

Dashboard (post-login landing page: task summary, unread threads, recent activity), Shared inbox
(thread list + conversation panel with template-insert/schedule-send/Reminder-SMS composer,
real-time via SignalR), Task board (type/description/priority/status/active, filters, forwarding,
recurrence, escalation), Templates (create/edit, bookmarks, Active/Inactive), SMS History
(Admin-only report: sender/status/scheduled/expiry/PDU-count per message, filterable), Audit log
(Admin sees all actors, Staff sees only their own actions), Settings
(General/SMS/Task/Template/Notification/User Management/System tabs — Quiet Hours, per-patient
rate limiting, Reminder SMS defaults, task forwarding rules, and user Active/Inactive/OnLeave
management with leave dates all live here). See `PROJECT_CONTEXT.md` §6b for the original spec
per screen, and `STATUS.md`'s Step 8/Step 9 checklists for everything added on top of it.

## Tests

```
dotnet test NotifyHub.sln --filter "Category!=MySql"   # fast suite: 190 tests (86 Domain + 104 Integration), InMemory EF Core
dotnet test NotifyHub.sln --filter "Category=MySql"     # real-MySQL race test (needs docker compose up -d mysql)
dotnet test NotifyHub.sln                                # everything CI runs
```

**Domain coverage (FR-013): 94.2% line coverage** on `NotifyHub.Domain` — comfortably above the
70% requirement. Methodology, per-class breakdown, and how to regenerate the number:
[`docs/coverage/DOMAIN_COVERAGE.md`](docs/coverage/DOMAIN_COVERAGE.md).

End-to-end (Playwright, 11 tests): requires the full stack running. **Known-stale since step 8's
increment 13** — the suite's login helper still waits for a `/inbox` redirect that no longer
happens (post-login now lands on `/`, the Dashboard), so every spec currently fails at login;
see `STATUS.md`'s Step 9 checklist. Fixing it wasn't in `STEP9_PLAN.md`'s scope.

```
docker-compose up -d
cd notifyhub-web && npm run test:e2e
```

## CI

`.github/workflows/ci.yml` runs on every push: restore/build/test the .NET solution (with a real
MySQL service container for the integration suite) and build the frontend. Green build is required
before merge (FR-014).

## Documentation index

| Doc | What it covers |
|---|---|
| [`PROJECT_CONTEXT.md`](PROJECT_CONTEXT.md) | Locked requirements, data model, API spec, business rules — source of truth for scope |
| [`CODEBASE_MAP.md`](CODEBASE_MAP.md) | What's actually implemented, with file:line citations — read this before making changes |
| [`STATUS.md`](STATUS.md) | Build log: what shipped in each step, deviations from spec (with reasons), known limitations, change log |
| [`docs/adr/`](docs/adr/) | 3 ADRs (FR-016): outbound queue (MySQL table vs. RabbitMQ/Redis), dispatcher hosting (BackgroundService vs. Windows Service), RBAC model (2 roles vs. broader) — each with rejected alternatives and why |
| [`docs/SECURITY.md`](docs/SECURITY.md) | OWASP Top-10 self-assessment + FR-018 sub-criteria (a)–(e) |
| [`docs/AI_USAGE_LOG.md`](docs/AI_USAGE_LOG.md) | AI usage log (FR-019): phases, representative sessions, one "AI was wrong" example + fix, one example of AI used beyond code generation |
| [`docs/coverage/DOMAIN_COVERAGE.md`](docs/coverage/DOMAIN_COVERAGE.md) | FR-013 coverage number + methodology |

## Deviations from the literal spec, and known limitations

Every deliberate deviation from `PROJECT_CONTEXT.md` (with the reasoning behind it) and every known
limitation left by design (not a bug) is tracked in `STATUS.md`'s "Documented deviations" and
"Known limitations" sections rather than repeated here — check there before assuming a gap is
unintentional.

## Git history (FR-012)

Built incrementally, one numbered step at a time (skeleton → auth → outbound pipeline → inbound
routing/tasks → reminders → audit/seed → docs), each with its own commits and, where live-tested,
its own bug-fix commits — no single "final version" commit. See `git log` or `STATUS.md`'s change
log for the step-by-step narrative.
