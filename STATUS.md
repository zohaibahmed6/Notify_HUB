# NotifyHub — Build Status

## Current step
Step 2 of 6 — Outbound messaging pipeline (templates, mock gateway, dispatcher, retry/backoff, audit) — **code complete, pending your review** (see "Needs your verification" below before moving to step 3).

## Step 1 checklist (reviewed)
- [x] Solution skeleton, EF Core + MySQL, JWT auth (login/refresh/RBAC), seed scaffolding, Docker/compose, CI, React login screen
- [x] Reviewed by Zohaib — Docker stack, Admin + Staff login, JWT role claims all verified working
- [x] Swagger JWT bearer auth wired (`AddSecurityDefinition`/`AddSecurityRequirement`) — Authorize button now available for testing protected endpoints directly in Swagger
- Note: RBAC *enforcement on a real endpoint* wasn't testable in step 1 (no protected business endpoint existed yet — `/api/audit` lands in step 5). Step 2 doesn't add a JWT-protected business endpoint either (`/api/templates` is JWT-protected but open to both roles); RBAC-by-role enforcement remains meaningfully testable starting step 5.

## Step 2 checklist
- [x] Domain: `Patient`, `Appointment`, `MessageTemplate`, `OutboundMessage`, `DeliveryStatusHistory`, `AuditLog` entities; `MessageStatus`/`TriggerType`/`SenderType`/`AppointmentStatus` enums
- [x] Domain: `TemplateRenderer` (merge-field substitution), `IdempotencyKeyGenerator` (SHA-256), `RetryBackoffPolicy` (1/2/4/8/16 min, 6-attempt cap: 1 initial send + 5 retries) — pure, unit-tested
- [x] EF Core configs + migration (`AddOutboundPipeline`) for all six new tables + required indexes
- [x] Idempotent seed steps: 10 synthetic patients + appointments, 4 templates (2 appointment-reminder offsets + medication + prescription), a handful of demo queued messages so the pipeline has work at startup
- [x] Mock SMS gateway (`POST /api/mock-gateway/send`): random delay/fail per config, then calls its own webhook receipt endpoint
- [x] Webhook receipt (`POST /api/webhooks/gateway-receipt`, shared-secret authenticated): delivered → terminal; failed → retry with backoff or terminal at 6 attempts (BR-011, clarified 2026-07-11: 1 initial + 5 retries)
- [x] Audit logging (`AuditLogger`): "send" and "receipt" events, actor + timestamp (FR-011, partial — opt-out/assignment/escalation land with the features that produce them)
- [x] `GET/POST /api/templates` (Admin or Staff, per §8)
- [x] Real `DispatcherWorker` (Worker project) replacing the step-1 placeholder heartbeat: polls due messages, renders at send time (BR-013), calls the gateway, requeues with backoff on transport failure
- [x] Config wired end-to-end: appsettings, `.env`/`.env.example`, docker-compose
- [x] Tests: 13 new Domain unit tests (renderer/idempotency/backoff) + 3 new integration tests (happy-path delivery, single retry, 6-attempt terminal failure). **41/41 passing** (28 Domain, 13 Integration)

## What's implemented
- **Domain**: entities per §7 (see checklist). `TemplateRenderer.Render` substitutes `{{field}}` tokens, leaving unresolved ones as-is rather than throwing. `IdempotencyKeyGenerator.Generate` = SHA-256 hex of `patientId:templateId:triggerReference` (BR-009: differs on reschedule). `RetryBackoffPolicy`: `IsTerminal`/`NextDelay` implement the 1/2/4/8/16-minute exponential schedule, capped at 6 total attempts (1 initial + 5 retries) — all 5 backoff values are reachable.
- **Infrastructure**: EF configs for all six new tables (`ToTable`, keys, `HasConversion<string>` on enums, following the existing `UserConfiguration` pattern). `outbound_messages` has the required `(status, next_retry_at)` index for FR-010; `idempotency_key` is a unique index (nullable-safe — MySQL allows multiple NULLs). `PatientAppointmentSeedStep`/`TemplateSeedStep`/`DemoOutboundMessageSeedStep` — each independently idempotent (skip if any row of that type exists already), registered after `UserSeedStep` so later steps can see earlier-seeded data. `MessageDispatcher` (testable independently of the Worker host): claims a batch of due messages, renders each (patient name + appointment time, resolved by parsing `trigger_reference` for appointment-linked triggers), calls the gateway, and on a transport-level failure requeues with backoff exactly like a gateway-reported failure does.
- **Api**: `MockGatewayController` (`POST /api/mock-gateway/send`) — marks the message Sent, then awaits a random delay (`MockGateway:Min/MaxDelayMs`) and a random outcome (`MockGateway:FailRatePercent`) before POSTing the receipt to its own webhook endpoint. `WebhooksController` (`POST /api/webhooks/gateway-receipt`) — updates status, writes `delivery_status_history`, applies BR-011's retry/terminal logic, audits. `TemplatesController` — list/create, validated (`Body` non-empty ≤1000 chars, `OffsetHours` positive integer, `TriggerType` parsed against the enum). `SharedSecretAttribute` — constant-time comparison against `Webhooks:SharedSecret`, paired with `[AllowAnonymous]` on both gateway/webhook endpoints (neither carries a JWT). Swagger now has a Bearer auth definition + global security requirement (Authorize button).
- **Worker**: `DispatcherWorker` — 5-second poll loop calling `MessageDispatcher.DispatchDueMessagesAsync`, with its own error-retry backoff on poll-cycle failure (mirrors the step-1 placeholder's resilience posture). `PlaceholderHeartbeatWorker` removed (superseded).
- **Tests**: `TemplateRendererTests`, `IdempotencyKeyGeneratorTests`, `RetryBackoffPolicyTests` (Domain). `OutboundPipelineHappyPathTests`/`OutboundPipelineRetryTests` (Integration) — exercise FR-013's required "queue→dispatch→gateway→webhook→status update" workflow via two factory subclasses that fix `MockGateway:FailRatePercent` to 0 or 100 (per §11's own stated test-design decision) for determinism.

## Needs your verification (could not run in this build environment — still no Docker/MySQL here)
1. **`docker-compose up` on a fresh volume** — confirm the new `AddOutboundPipeline` migration applies cleanly alongside `InitialCreate`, and that all four seed steps run without error (check for "Running seed step ..." log lines from `DbSeedRunner`, one per step, in order).
2. **Dispatcher activity** — within ~5–10 seconds of the stack coming up, `worker` logs should show `"Dispatcher: processed N due message(s)"` (N > 0, from the demo-seeded messages), and the `outbound_messages`/`delivery_status_history`/`audit_log` tables should show movement (query them, or watch `api` logs for the mock-gateway/webhook calls). Given `MockGateway:FailRatePercent=10`, expect most demo messages to end up `Delivered` and roughly 1 in 10 cycling through a retry.
3. **Swagger Authorize button** — `/swagger`, click Authorize, paste an access token from `/api/auth/login`, confirm `/api/templates` (POST) now works from the Swagger UI without a 401.
4. **Restart idempotency** — restart the stack against the same volume; confirm no duplicate patients/templates/demo messages appear (each seed step's "skip if already seeded" check).

## Documented deviations from PROJECT_CONTEXT.md
- **`outbound_messages.thread_id` exists as a plain nullable column with no FK yet.** Threads don't exist until step 3; the column is already in place (matching §7's schema) so step 3 only needs to add the FK constraint, not a new column.
- **Mock gateway posts its receipt via a real HTTP self-call** (`Api` → its own `/api/webhooks/gateway-receipt`), not an in-process shared method call — this exercises the shared-secret auth path for real (relevant to FR-018's security self-assessment), and stays fully synchronous/awaited (no fire-and-forget, no polling) so both the demo and the integration tests are deterministic.
- **`Webhooks:SharedSecret` is reused to authenticate the Worker → Api mock-gateway call**, not a separate secret. Both are service-to-service calls that can't carry a JWT; introducing a second secret for this seemed like unnecessary sprawl for a 3-day build. Flag if you'd rather these be independently rotatable.
- **`attempt_count` counts failed attempts only**, not total attempts including the eventual success — it increments only when the gateway (or the transport call to it) fails. BR-011 (clarified 2026-07-11): 6 total attempts allowed (1 initial send + 5 retries), terminal `Failed` at `attempt_count == 6`. A message delivered on its first try stays at `attempt_count = 0`.
- **Non-appointment templates use `OffsetHours = 1`, not 0.** §9 states offset hours must be a positive integer; medication/prescription alerts don't conceptually have a "before/after" offset the way appointment reminders do, but the schema doesn't distinguish, so seeded them at the minimum valid value (1) rather than violating the stated validation rule.
- **No manual "create outbound message" API endpoint added.** Nothing in §8 calls for one, and the real trigger (reminder scheduler, FR-009) is step 4/5 work. The demo-seeded queued messages give the dispatcher something to process at startup in the meantime; the integration tests create/claim messages directly via `DbContext`, the same way a future trigger would.

## Known limitations (by design, not bugs)
- `sender_type` is always `System` for now — ad-hoc staff replies (the other value) don't exist until threads/inbox land in step 3.
- No stale-"Sending"-message recovery sweep. If the Worker crashes between claiming a message (Queued→Sending) and the gateway call completing, that message stays `Sending` forever rather than being auto-requeued. Given the mock gateway call is synchronous and sub-second, this window is tiny; a real crash-recovery sweep is deferred as out of scope for a single-instance mock system (§6d already documents multi-worker locking as out of scope for the same reason).
- `{{appointment_time}}` is resolved by parsing `trigger_reference` for an `appointment:{id}:...` prefix — there's no `appointment_id` column on `outbound_messages` in §7's schema, so this was the only available link back to the appointment at render time.

## How to run
```
docker-compose up
```
Requires a `.env` file at the repo root — copy `.env.example` and fill in values (see `.env.example` for the full list of required keys). Two new keys since step 1: `MOCKGATEWAY__CALLBACKBASEURL` (Api's self-callback) and `MOCKGATEWAY__APIBASEURL` (Worker → Api).

Seeded accounts (values from your local `.env`, not committed):
- Admin: `SEED__ADMINUSERNAME` / `SEED__ADMINPASSWORD`
- Staff: `SEED__STAFFUSERNAME` / `SEED__STAFFPASSWORD`

## Open questions
- None currently blocking.

## Change log
| Date | Step | Summary |
|---|---|---|
| 2026-07-11 | 1 | Solution skeleton, EF Core + MySQL, JWT auth (login/refresh/RBAC), seed scaffolding, Docker/compose, CI, React login screen. 20/20 tests passing. Reviewed and verified working by Zohaib. |
| 2026-07-11 | 1 (fix) | Swagger JWT bearer security definition added per review feedback (Authorize button). |
| 2026-07-11 | 2 | Outbound messaging pipeline: templates, mock gateway, webhook receipt, retry/backoff (BR-011), audit logging (send/receipt), real Worker dispatcher, seed data for patients/appointments/templates/demo messages. 40/40 tests passing (27 Domain, 13 Integration). Docker-compose smoke test pending user review (no Docker/MySQL in build environment). |
| 2026-07-11 | 2 (fix) | BR-011 clarified per PROJECT_CONTEXT.md update: 6 total attempts (1 initial + 5 retries), all 5 backoff values used. `RetryBackoffPolicy.MaxAttempts` 5→6; tests updated. Also fixed a test-isolation bug in `OutboundPipelineRetryTests` where a prior test's requeued message could be re-claimed by a later test. 41/41 tests passing (28 Domain, 13 Integration). |
