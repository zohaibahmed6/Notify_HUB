# ADR 0001 — Outbound message queue: MySQL table vs. RabbitMQ/Redis

**Status:** Accepted (Session 1, approved by Zohaib — see `PROJECT_CONTEXT.md` §14)

## Context

FR-001–004 require a queued/dispatched outbound messaging pipeline: messages are created in a
`queued` state, picked up by a worker, sent through a mock gateway, and their delivery status
tracked through to `delivered`/`failed`. FR-003/BR-002 require the pipeline to survive a
kill/restart mid-batch with no duplicate sends, and BR-011 requires a bounded retry-with-backoff
schedule. This needs some kind of queue: an ordered, poll-or-push work list that a worker can claim
items from.

Two broad approaches were available: a table inside the existing MySQL database
(`outbound_messages`, polled by the worker), or a dedicated message broker (RabbitMQ or Redis
streams/lists) alongside MySQL.

## Decision

**Use a plain MySQL table (`outbound_messages`) as the queue**, polled by `DispatcherWorker`
(`NotifyHub.Worker/DispatcherWorker.cs`) every 5 seconds, claiming a batch of up to 10 `Queued`
rows ordered by `CreatedAt` (`MessageDispatcher.DispatchDueMessagesAsync`,
`NotifyHub.Infrastructure/Messaging/MessageDispatcher.cs:19-35`). Status transitions
(`Queued→Sending→Sent/Delivered/Failed`) and retry scheduling (`NextRetryAt`,
`OutboundMessageConfiguration.cs`'s `(Status, NextRetryAt)` index) live as columns on the same row —
there is no separate broker, exchange, or topic.

## Rejected alternatives

- **RabbitMQ.** Rejected because: (1) it's a second stateful service to run, health-check, and
  reason about in a 3-day solo build with a hard one-command-run requirement (FR-015) — every
  extra moving part is extra Docker Compose surface area and extra failure modes to debug under
  time pressure; (2) durable delivery-status history (FR-004, "queryable per message") and
  idempotency-key uniqueness (FR-003) are naturally relational — modeling them in MySQL either way
  means the "source of truth" for a message's state ends up in the database regardless of whether
  a broker also holds a copy, so a broker would be a second system to keep consistent with the
  first, not a replacement for it; (3) RabbitMQ doesn't give it anything the table-with-status-
  column approach doesn't already provide at this scale (a few thousand messages a day, not
  millions), so the operational cost isn't bought back by a real throughput or fan-out need.
- **Redis (streams or list-based queue).** Rejected for the same core reason — it would need to
  either duplicate message state that also has to live in MySQL for FR-004's queryable history, or
  become the sole source of truth and lose MySQL's ACID guarantees / EF Core tooling for the
  business-rule-heavy retry/status logic (BR-011's 6-attempt cap, the idempotency-key unique
  constraint enforced at the DB level per §9). Redis's actual strengths (sub-millisecond ops,
  pub/sub fan-out) aren't the bottleneck this pipeline has; SignalR already covers the real-time
  fan-out need (FR-007) without Redis's backplane.

## Consequences

- **Positive:** one dependency (MySQL) instead of two; `docker-compose up` stays simple (FR-015);
  the idempotency key's uniqueness is a real DB constraint, not an application-level check that
  could race; delivery-status history and the queue are the same row, so there's no
  broker/database consistency problem to solve.
- **Negative, documented as a known limitation (`STATUS.md`):** no built-in row-locking across
  multiple worker instances — a second `Worker` replica polling the same table could double-claim a
  row unless `SELECT ... FOR UPDATE SKIP LOCKED` (or equivalent) is added first. Out of scope here
  because the architecture runs exactly one `Worker` instance (§2/§6d); would need to be addressed
  before horizontally scaling the dispatcher.
- Polling (5s fixed interval, `DispatcherWorker.cs:10`) means up to ~5s of added latency per
  message compared to a push-based broker — acceptable for SMS reminders/alerts, not acceptable for
  a use case needing sub-second delivery.
