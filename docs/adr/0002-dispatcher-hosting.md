# ADR 0002 — Dispatcher hosting: in-process `BackgroundService` vs. a real Windows Service

**Status:** Accepted (Session 1, approved by Zohaib — see `PROJECT_CONTEXT.md` §14/§2)

## Context

FR-001–004/008/009 all need something running continuously in the background, independent of any
inbound HTTP request: the message dispatcher (polls `outbound_messages`), the escalation job (polls
overdue tasks), and the reminder scheduler (polls due appointments). The assessment brief specifies
ASP.NET Core Web API and Docker/docker-compose ("one-command run", FR-015) but doesn't mandate a
specific hosting model for background work.

Two realistic options: host the recurring jobs as `BackgroundService`s inside a plain .NET worker
process (`NotifyHub.Worker`, itself just another container in the compose stack), or build them as
an actual Windows Service (or three), installed via `sc create`/a Windows-specific installer,
running directly on a host OS rather than in a container.

## Decision

**`NotifyHub.Worker` is a separate .NET project hosting three `BackgroundService`s**
(`DispatcherWorker`, `EscalationWorker`, `ReminderWorker` — `NotifyHub.Worker/*.cs`), each a
long-running polling loop (`DispatcherWorker.cs:8-35` etc.), run as its own container in
`docker-compose.yml` alongside `api`/`mysql`/`web`.

## Rejected alternatives

- **Real Windows Service(s).** Rejected because: (1) it's directly incompatible with FR-015's
  "one-command run" via `docker-compose up` — a Windows Service has to be installed on a Windows
  host via `sc.exe`/an MSI, which can't be expressed as a Docker Compose service and can't run on
  the Linux CI runner GitHub Actions uses (`ubuntu-latest`, `.github/workflows/ci.yml:8`); (2) it
  would fork the codebase's runtime model in two — the exact same "poll `outbound_messages` on an
  interval" logic would need either a Windows-Service-specific host (`Microsoft.Extensions.Hosting.
  WindowsServices`) alongside a container-friendly one for CI/demo purposes, or the whole system
  would only run on Windows, which contradicts using Docker at all; (3) nothing about the actual
  job logic (poll, claim, dispatch, retry) needs OS-level service semantics (auto-start on boot,
  Windows Event Log integration, SCM-managed recovery) that a `BackgroundService` inside a
  container doesn't already get for free from Docker's own restart policy.
- **Running the same polling loops inside the `NotifyHub.Api` process itself** (no separate
  `Worker` project at all). Considered but rejected: a crash or slow request-handling thread in the
  Api process would then also risk starving or crashing the dispatcher, and vice versa — a runaway
  background job would degrade API latency for every request. Splitting them into separate
  processes/containers means the Api staying responsive and the Worker doing its polling are
  independent failure domains, and either can be scaled, restarted, or redeployed without touching
  the other.

## Consequences

- **Positive:** identical hosting model in every environment (dev machine, CI, docker-compose
  demo) — a `BackgroundService` is just a .NET `IHostedService`, no OS-specific installation step
  anywhere; trivially testable (`EscalationJobTests`/`MessageDispatcherOptOutTests` instantiate the
  job classes directly against a `DbContext`, no need to spin up a real service host at all, per
  `CODEBASE_MAP.md` §7).
- **Documented tradeoff, not a gap:** this is explicitly a "Windows Service production equivalence"
  ADR, not a claim that `BackgroundService` is strictly better in every deployment. In a real
  production Windows-hosted deployment (outside this assessment's Docker-first scope), the same
  `NotifyHub.Worker` executable could be re-hosted via `UseWindowsService()` with no change to the
  job logic itself — the polling loops don't depend on being inside a container, only on
  `IHostedService`'s lifecycle, so the migration path exists without a rewrite if it's ever needed.
- **Negative, already logged (`STATUS.md` known limitations):** the Worker doesn't gate its first
  poll on the Api's EF Core migration completing (no health-check endpoint to gate on yet) — it
  self-heals via the existing 5s error-retry delay, producing a few seconds of expected
  "table doesn't exist" warnings at cold start. Acceptable for a demo; would want an explicit
  readiness check before a real production rollout.
