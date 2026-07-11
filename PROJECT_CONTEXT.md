# NotifyHub — Project Context

*Single source of truth for this build. Source: AI Foundation Team Assessment Pack (July 2026) + Zohaib Ahmed Addendum (HR 2363). All future implementation, design, or scope decisions must be checked against this document before code changes.*

**Status:** Locked — ready for implementation
**Deadline:** Hard 3-day deadline (assessment pack nominally allows 40h/10 working days — compressed here; scope prioritized accordingly)
**Declared AI tools (must be the only tools used/logged):** Claude, ChatGPT

---

## 1. Project overview

**Purpose:** Multi-channel patient messaging platform — templated outbound SMS pipeline, two-way patient inbox, task orchestration — built solo in 3 days to demonstrate senior engineering ability plus effective AI-tool orchestration.

**Scope:** All Must-priority functional requirements below (FR-001 to FR-019).

**Out of scope:** Patient/provider/appointment management UI (stub data only, no dedicated screens); real SMS carrier integration; production deployment; leave-management and quiet-hours/rate-limiting stretch goals (explicitly dropped, not deferred).

**Success criteria:** All Must-have FRs functional end-to-end; all common requirements (tests, CI, docs, security, AI log) complete; target rubric score 65+.

**Reused assets:** None. Per explicit decision, no code or screens are reused from any prior personal or company project (including "Aura Clinic"). Everything is built fresh in this repository during the assessment window.

---

## 2. Technology stack

| Layer | Technology | Source |
|---|---|---|
| Backend framework | ASP.NET Core Web API | Assessment brief (explicit) |
| Database | MySQL | Assessment brief (explicit) |
| ORM | Entity Framework Core | Inference — parameterized queries required for FR-018 |
| Background jobs | ASP.NET Core `BackgroundService` (separate `Worker` project) | Decided in chat — dockerizable, CI-simple; ADR documents Windows Service production equivalence |
| Real-time | SignalR | Assessment brief (explicit) |
| Frontend | React | Decided in chat — brief requires moving beyond jQuery patterns |
| Auth | JWT | Assessment brief (explicit, common requirements) |
| Testing | xUnit | Decided in chat |
| CI | GitHub Actions | Assessment brief (explicit) |
| Containerization | Docker + docker-compose | Assessment brief (explicit — "one-command run") |
| API docs | Swagger/OpenAPI (Swashbuckle) | Assessment brief (explicit) |

### Solution structure
```
NotifyHub.sln
├── NotifyHub.Api/            → REST endpoints, SignalR hub, Swagger, auth
├── NotifyHub.Worker/         → BackgroundService: dispatcher, reminder scheduler, escalation job
├── NotifyHub.Domain/         → Entities, rules engine, state machines (no EF/HTTP deps — unit tested)
├── NotifyHub.Infrastructure/ → EF Core, MySQL, repositories, mock SMS gateway
├── NotifyHub.Tests/          → xUnit: Domain.Tests, Integration.Tests
└── notifyhub-web/            → React app
```

---

## 3. Functional requirements

| ID | Description | Priority | Type | Acceptance criteria |
|---|---|---|---|---|
| FR-001 | Templated outbound messaging, queued and dispatched | Must | Requirement | At least 3 trigger types functional: appointment reminder, medication alert, prescription alert. Templates support `{{field}}` merge syntax (e.g. `{{patient_name}}`, `{{appointment_time}}`), rendered server-side at send time; rendered text is snapshotted onto outbound_messages.rendered_body (BR-013). Message created → queued → dispatched by worker |
| FR-002 | Mock SMS gateway: random delay/fail, webhook delivery receipt | Must | Requirement | Gateway endpoint delays/fails/succeeds randomly; posts receipt via webhook |
| FR-003 | Idempotent dispatch, retry with backoff | Must | Requirement | Kill/restart worker mid-batch → no duplicate sends (proven by test). Idempotency key = deterministic hash of (patient_id + template_id + trigger_reference), where trigger_reference encodes the specific business event — e.g. `appointment:{id}:created`, `appointment:{id}:rescheduled:{version}` — not just the appointment ID, so a legitimate reschedule reminder is not blocked as a duplicate of the original. Backoff schedule: exponential — 1min, 2min, 4min, 8min, 16min across the 5 attempts allowed by BR-011 |
| FR-004 | Delivery status history (queued→sent→delivered/failed) | Must | Requirement | Status history queryable per message |
| FR-005 | Inbound webhook → routed to correct patient thread | Must | Requirement | Reply creates/updates thread |
| FR-006 | STOP/opt-out handling, auditable | Must | Requirement | Opted-out patient receives no further sends; enforced server-side, logged. Matches keyword variants case-insensitively: STOP, UNSUBSCRIBE, CANCEL, END, QUIT |
| FR-007 | Shared inbox, SignalR real-time across 2+ sessions | Must | Requirement | Two open browser tabs see live update |
| FR-008 | Task engine: message→task, priority, due date, overdue escalation, recurring | Must | Requirement | Overdue task auto-reassigns per configured rule. Completing a recurring task creates the next occurrence per the recurrence design in BR-007 (due-date-anchored, completion-only, optional end/max, reassigned to original owner). Default priority on creation: `medium`; default due date: system-suggested by priority (urgent=4h, high=1d, medium=3d, low=7d from creation time) — both are staff-overridable before saving. Escalated status auto-reverts to in_progress on any action taken by the assignee (BR-014) |
| FR-009 | Reminder scheduling (48h/2h before appointment), re-run safe | Must | Requirement | Re-running scheduler creates no duplicate reminders |
| FR-010 | Seed 50k messages, paginated/indexed inbox | Must | Requirement | Inbox list performant at scale; indexes documented |
| FR-011 | Full audit log — 5 explicit event types | Must | Requirement | Each of: send, delivery receipt, opt-out, thread assignment, task escalation individually logged with actor + timestamp |
| FR-012 | Incremental git commit history | Must | Requirement (common) | No single "final version" commit |
| FR-013 | Tests ≥70% domain coverage + 1 integration test | Must | Requirement (common) | Coverage report + passing integration test of primary workflow (queue→dispatch→gateway→webhook→status update) |
| FR-014 | CI pipeline (build+test on push) | Must | Requirement (common) | Green GitHub Actions run on every push |
| FR-015 | One-command run (docker-compose) | Must | Requirement (common) | `docker-compose up` boots full system + seed data |
| FR-016 | README + 3 ADRs with rejected alternatives | Must | Requirement (common) | Each ADR documents: decision, rejected alternatives, why rejected. Three ADRs required: (1) outbound queue — MySQL table vs. RabbitMQ/Redis, (2) dispatcher hosting — in-process BackgroundService vs. real Windows Service, (3) RBAC model — two roles (Admin/Staff) vs. broader role set |
| FR-017 | Swagger/OpenAPI docs | Must | Requirement (common) | `/swagger` reachable, endpoints documented |
| FR-018 | Security baseline + OWASP self-assessment | Must | Requirement (common) | Sub-criteria: (a) authN/RBAC enforced server-side, (b) input validation on every endpoint incl. webhooks, (c) parameterized data access only, (d) secrets in config/env not code, (e) OWASP Top-10 self-assessment doc |
| FR-019 | AI usage log (2-4 pages) | Must | Requirement (common) | Phases covered (design/scaffold/implement/test/debug/docs); 3 representative sessions with ≥1 frontend example; 1 "AI was wrong/unsafe" example + fix; 1 example of AI used beyond code gen; tools logged match declared list (Claude, ChatGPT only) |

**Explicitly dropped (not deferred):** Leave-management exclusion, quiet hours, per-patient rate limiting — stretch goals, out of scope for this build.

---

## 4. User roles & permissions

| Role | Permissions | Restrictions |
|---|---|---|
| Admin | Full access: templates, threads/tasks, audit log, all data | — |
| Staff | Assigned threads, reply, create/complete tasks, manage templates, view own audit trail | Cannot manage users |

Users seeded via script — no dedicated user-management UI (decision: only must-have screens built).

---

## 5. Business rules

| ID | Rule |
|---|---|
| BR-001 | Opted-out patients (STOP) never receive future sends — system-enforced, not just UI-hidden. Enforced at two points: (a) dispatcher checks patients.opt_out_at immediately before calling the gateway, not only at message-creation time, so a STOP arriving after a message is queued still blocks it; (b) applies to both system-dispatched and staff ad-hoc messages — once opted out, the frontend disables the reply Send button with a visible "patient opted out" banner |
| BR-002 | A crashed/restarted dispatcher must never double-send a message |
| BR-003 | Re-running the reminder scheduler must not create duplicate reminders |
| BR-004 | Overdue tasks are automatically reassigned and flagged per configured threshold — "overdue" = the instant `now > due_at`, no grace period; evaluated on each escalation job run. Reassignment target: Admin (acts as fallback owner/triage point); Admin may manually reassign further. Escalating a task does not change original_owner_id (see BR-007d) |
| BR-005 | All role checks enforced server-side, not just hidden UI |
| BR-006 | All data synthetic; no real patient data; no code copied from company repositories or reused from prior personal projects |
| BR-007 | Recurrence design: (a) due-date-anchored — next_due_at = previous_due_at + recurrence_interval_days, no drift; (b) only completing a task spawns the next occurrence — cancelling ends the series; (c) recurrence stops when recurrence_end_date is reached or occurrence_count exceeds recurrence_max_occurrences (both optional — unbounded if neither set); (d) next occurrence is always assigned to original_owner_id, regardless of who the previous occurrence was escalated to or completed by |
| BR-008 | A staff-authored ad-hoc reply is not tied to a template; conversation view must render both templated (system) and ad-hoc (staff) messages together in thread order |
| BR-009 | trigger_reference (used in the idempotency key, FR-003) encodes the specific business event, not just an entity ID — e.g. an appointment reschedule produces a new trigger_reference and is therefore a legitimate new send, not a blocked duplicate |
| BR-010 | When an appointment is rescheduled, any still-`queued` reminder tied to the old trigger_reference is marked `superseded` (terminal, not sent); the reminder scheduler creates fresh reminders under the new trigger_reference |
| BR-011 | A message stops retrying after 6 total attempts (1 initial send + 5 retries) and moves to a terminal `failed` status; all 5 backoff values from FR-003 (1/2/4/8/16 min) are used, one between each attempt. This is audited |
| BR-012 | Thread assignment only succeeds if `assigned_staff_id` is currently null; a concurrent assign attempt on an already-assigned thread returns 409 Conflict |
| BR-013 | Template merge fields (`{{field}}` syntax) are rendered server-side at send time using the specific patient/appointment/message context; the rendered text is stored on outbound_messages.rendered_body — not re-rendered live from the template — so audit history reflects what was actually sent even if the template is edited afterward |
| BR-014 | A task's `escalated` status auto-reverts to `in_progress` the moment any action is taken on it (opened, updated, or reassigned) by the current assignee — it does not require a separate manual status change |

---

## 6a. Frontend architecture

| Concern | Decision |
|---|---|
| Styling/components | shadcn/ui (Tailwind + accessible pre-built components) |
| Data fetching/cache | TanStack Query (React Query) |
| Routing | React Router — one route per screen (§6) |
| HTTP client | `fetch`, wrapped in a single `apiClient` module (base URL from env var, attaches JWT header, handles 401 → redirect to login) |
| Auth token storage | In-memory (React context) + refresh via dedicated `POST /api/auth/refresh` on expiry (refresh token rotated each call, not re-sent password); not localStorage, to reduce XSS exposure |
| API base URL | `VITE_API_URL` env var, set per environment in docker-compose |
| CORS | API allows only the web container's origin (docker-compose service name / configured frontend URL), not wildcard |
| SignalR client | `@microsoft/signalr` client, connects to `/hubs/inbox` with the JWT on connection; reconnect-on-drop enabled |
| Build tool | Vite |

---

## 6b. Screen list

| Screen | Purpose | Main components | Actions | Validation |
|---|---|---|---|---|
| Login | Auth entry | Username/password form | Submit → JWT + role redirect | Required fields, error on bad creds |
| Shared inbox | Core messaging UX | Thread list, conversation panel | Reply, assign, make task | Non-empty message body |
| Task board | Track follow-ups | List/board, priority+due badges | Create, complete, reassign | Due date required, priority enum |
| Templates & reminder rules | Configure messaging | Template form, rule table | Create/edit template, set offset hours | Body non-empty, max 1000 chars; offset > 0 |
| Audit log | Compliance/traceability | Filterable table | Filter by actor/action/date | Read-only |

---

## 6c. UX behaviors (QA-identified gaps, now specified)

| Behavior | Rule |
|---|---|
| Empty inbox (no threads) | Invitation-style empty state: headline names the space, one-line body, no "nothing here yet" |
| Empty thread (assigned, zero messages) | Same pattern — prompt to send first message |
| Empty task board | Same pattern |
| Real-time message arrives while staff has a draft reply open | New message appends to thread; draft box is untouched; auto-scroll only if the view was already at the bottom |
| Unread count decrement | Resets to 0 on `GET /api/threads/{id}` (opening the thread) |
| Task priority display | Color badge always paired with a text label — never color alone |
| Action confirmations/errors | Toast notification, past tense, no "successfully" (e.g. "Task created", "Message sent") — consistent across all 5 screens, not per-screen ad hoc |

---

## 6d. Documented limitations (deliberately not built — note in relevant ADR)

- **Multi-worker dispatcher locking:** architecture runs a single `Worker` instance (§2); `SELECT ... FOR UPDATE SKIP LOCKED` or equivalent row-locking is not implemented since no concurrent-worker scenario exists in this deployment. Would be required before horizontally scaling the Worker.
- **Client-side template body character counter:** validation is server-side only (§9); a live counter is UX polish with no rubric weight — build only if Day 3 has slack.

---

| Entity | Key fields | Relationships |
|---|---|---|
| `patients` | id, name, phone (unique), opt_out_at | 1—N appointments, threads |
| `appointments` | id, patient_id (FK), scheduled_at, status | Stub only — feeds FR-009 |
| `message_templates` | id, name, body (max 1000 chars, supports `{{field}}` merge syntax — e.g. patient_name, appointment_time), trigger_type, offset_hours | 1—N outbound_messages |
| `outbound_messages` | id, patient_id (FK), thread_id (FK, nullable — set when a thread exists), template_id (FK, **nullable** — null means ad-hoc staff reply), sender_type (system/staff), trigger_reference (nullable — business event string, null for ad-hoc), rendered_body (text — the actual merge-field-rendered content sent, snapshot per BR-013), created_at, status, idempotency_key (VARCHAR(64), SHA-256 hex of patient_id+template_id+trigger_reference, unique, required only for system-dispatched messages), attempt_count, next_retry_at | 1—N delivery_status_history |
| `delivery_status_history` | id, message_id (FK), status, occurred_at | — |
| `threads` | id, patient_id (FK, **UNIQUE** — one thread per patient, created via find-or-create to prevent race-condition duplicates), assigned_staff_id (FK), unread_count | 1—N inbound_messages, outbound_messages, tasks |
| `inbound_messages` | id, thread_id (FK), body, received_at | — |
| `tasks` | id, thread_id (FK), priority, due_at, status, assigned_staff_id (FK), original_owner_id (FK — set at creation, next recurrence always reassigns here), is_recurring, recurrence_interval_days (nullable, required if is_recurring), recurrence_end_date (nullable), recurrence_max_occurrences (nullable), occurrence_count (default 1) | — |
| `audit_log` | id, actor, action, entity_type, entity_id, occurred_at, detail | — |
| `users` | id, username (unique), password_hash, role | — |
| `refresh_tokens` | id, user_id (FK), token_hash (unique), expires_at, revoked_at (nullable) | Supports rotation: issuing a new token sets revoked_at on the old one |

**Indexes (required for FR-010):** `outbound_messages(status, next_retry_at)`, `outbound_messages(thread_id, created_at)`, `inbound_messages(thread_id, received_at)`, `threads(assigned_staff_id)`.

**Password policy:** minimum 8 characters, high complexity (upper/lower/number/symbol).

---

## 8. API specification

| Method | Route | Auth | Purpose |
|---|---|---|---|
| POST | `/api/auth/login` | None | Issue JWT access + refresh token pair |
| POST | `/api/auth/refresh` | Refresh token (body) | Issue new access token; rotates refresh token (old one invalidated) |
| GET/POST | `/api/templates` | Admin or Staff | Manage message templates |
| GET | `/api/threads` | Staff/Admin | List threads (paginated) |
| GET | `/api/threads/{id}` | Staff/Admin | Thread detail + messages |
| POST | `/api/threads/{id}/messages` | Staff/Admin | Send staff reply |
| POST | `/api/threads/{id}/assign` | Staff/Admin | Assign to staff member (audited). Fails 409 if already assigned (BR-012) |
| POST | `/api/threads/{id}/tasks` | Staff/Admin | Convert message to task |
| GET/PATCH | `/api/tasks`, `/api/tasks/{id}` | Staff/Admin | List/update tasks |
| GET | `/api/audit` | Admin | Filtered audit log, all actors |
| GET | `/api/audit/mine` | Staff | Audit log filtered to the calling staff member's own actions (resolves §4's "view own audit trail" for Staff) |
| POST | `/api/webhooks/gateway-receipt` | Shared secret | Mock gateway → delivery status |
| POST | `/api/webhooks/inbound` | Shared secret | Simulated patient reply |
| Hub | `/hubs/inbox` | JWT | SignalR real-time channel |

---

## 9. Validation rules

Phone: synthetic E.164-like format. Template body: non-empty, max 1000 characters. Offset hours: positive integer. Task due date: required, present or future. Idempotency key: server-generated, unique constraint enforced at DB level.

---

## 10. Security requirements

- Auth: JWT with expiry + refresh — dedicated `/api/auth/refresh` endpoint, refresh tokens rotated on each use (old token revoked), stored hashed in `refresh_tokens`
- RBAC enforced server-side on every controller action
- Input validation on all endpoints, including inbound webhooks
- Parameterized queries via EF Core only — no raw SQL string concatenation
- Secrets via environment variables/config — never committed
- Webhook endpoints require shared-secret header (prevents spoofed delivery receipts / fake inbound replies)
- OWASP Top-10 self-assessment document required (FR-018)

---

## 11. Background processing

| Job | Trigger | Idempotency mechanism | Writes to audit_log (FR-011) |
|---|---|---|---|
| Dispatcher | Continuous poll on `outbound_messages` | Idempotency key check before send; max 5 attempts then terminal `failed` (BR-011) | Yes — "send" event on dispatch, "receipt" event on webhook callback |
| Reminder scheduler | Every 15 minutes | Checks for existing message before creating | Feeds dispatcher — same "send"/"receipt" events apply |
| Escalation job | Periodic | Only flags tasks past `due_at` not already escalated | Yes — "escalation" event; if task is auto-reassigned, also an "assignment" event (actor = system) |

**Manual actions also audited:** `POST /api/threads/{id}/assign` writes an "assignment" event (actor = staff user); STOP/opt-out handling (FR-006) writes an "opt-out" event at the point `patients.opt_out_at` is set — see BR-001, BR-006.

**Mock gateway configurability (Decision):** fail rate (%) and delay range (ms) are read from `appsettings.json` (`MockGateway:FailRatePercent`, `MockGateway:MinDelayMs`, `MockGateway:MaxDelayMs`), overridable per environment. Test project sets `FailRatePercent=0` for happy-path tests and `FailRatePercent=100` to deterministically exercise the retry path — required for the FR-013 integration test to be reliable, not flaky.

**Startup/deployment mechanics (Decision — closes an FR-015 implementation gap):**
- EF Core migrations apply automatically on API startup (`Database.Migrate()`).
- docker-compose: `mysql` service has a healthcheck; `api` and `worker` use `depends_on: condition: service_healthy` — prevents crash-looping on a cold database.
- Seed data (stub patients/appointments, 50k messages) loads via a one-time seed step that checks "is the DB already seeded" before running, so restarting the stack doesn't re-seed or duplicate data.

**Error response format (Decision):** all API errors return RFC 7807 `ProblemDetails` — consistent shape for the frontend's error handling.

---

## 11a. Implementation defaults (Inference — not stated in source documents, reasonable defaults to avoid inconsistent choices across files)

| Parameter | Default |
|---|---|
| Primary key type | `BIGINT AUTO_INCREMENT` on all tables |
| JWT access token expiry | 30 minutes |
| JWT refresh token expiry | 7 days |
| Pagination default page size | 25 (max 100) |
| Docker ports | api: 5000, web: 5173, mysql: 3306 (adjust freely — not graded) |
| Status enums — `outbound_messages.status` | `queued`, `sending`, `sent`, `delivered`, `failed` |
| Status enums — `tasks.status` | `open`, `in_progress`, `completed`, `escalated` |
| Status enums — `tasks.priority` | `low`, `medium`, `high`, `urgent` |
| Status enums — `message_templates.trigger_type` | `appointment_reminder`, `medication_alert`, `prescription_alert` |
| Timestamp storage | All timestamps stored UTC; frontend converts to local for display |

---

## 12. Non-functional requirements

Seed scale: 50,000 messages (explicit). Paginated + indexed inbox (explicit). Structured logging for audit/debug (inference — supports FR-011/FR-013). CI green on every push (explicit). No accessibility, browser-support, or uptime targets specified in source documents — not included.

---

## 13. Demo & evaluation readiness (checklist — not a build task)

- **15-min demo:** rehearsed walkthrough of core flow — outbound send→deliver, inbound reply→task creation
- **15-min code walkthrough:** evaluator picks any file, including AI-generated sections — be ready to explain line by line
- **15-min live extension task:** sealed until the session, performed live with Claude/ChatGPT while screen-sharing — no advance prep possible; stay fluent with these tools throughout the build, not just at the end

---

## 14. Change log

| Date/turn | Decision | Approved by |
|---|---|---|
| Session 1 | Frontend: React | Zohaib |
| Session 1 | Queue: MySQL-backed table | Zohaib |
| Session 1 | Dispatcher: in-process BackgroundService (not real Windows Service) | Zohaib |
| Session 1 | Solution structure: separate Api/Worker/Domain/Tests projects | Zohaib |
| Session 1 | RBAC: initially 3 roles, later reduced to 2 (Admin, Staff — Reporting dropped) | Zohaib |
| Session 1 | Test framework: xUnit | Zohaib |
| Session 1 | No reuse of "Aura Clinic" or any prior project code/screens | Zohaib |
| Session 1 | Screen list: 5 screens (login, inbox, tasks, templates, audit) — no user-management screen | Zohaib |
| Session 1 | Stretch goals (leave-mgmt, quiet hours/rate limiting) dropped entirely | Zohaib |
| Session 1 | Password policy: min 8 chars, high complexity | Zohaib |
| Session 1 | Webhook shared-secret auth: approved | Zohaib |
| Session 1 | Template body max length: 1000 characters | Zohaib |
| Session 1 | Reminder scheduler interval: 15 minutes | Zohaib |
| Session 1 | Staff role can manage templates (not Admin-only) | Zohaib |
| Session 1 | Gap fixes applied: multi-trigger-type FR-001, 5-event-type FR-011, ADR rejected-alternatives requirement, security sub-criteria, AI-log frontend requirement, demo-readiness section, 3-day constraint note | Zohaib |
| Session 1 | Build-order/unattended-operation instructions: declined — real-time supervision instead | Zohaib |
| Session 1 | Frontend architecture locked: shadcn/ui, TanStack Query, React Router, fetch+apiClient wrapper, in-memory JWT storage, Vite | Zohaib |
| Session 1 | ADR topics explicitly named: queue mechanism, dispatcher hosting, RBAC model | Zohaib |
| Session 1 | Idempotency key: deterministic hash of (patient_id + template_id + trigger_reference); trigger_reference encodes business event (create/reschedule/cancel), not just entity ID | Zohaib |
| Session 1 | Overdue definition: instant now > due_at, no grace period | Zohaib |
| Session 1 | Mock gateway fail rate/delay: configurable via appsettings, overridable for deterministic tests | Zohaib |
| Session 1 | Implementation defaults locked (IDs, JWT expiry, pagination, ports, status/priority enums) — tagged Inference | Zohaib |
| Session 1 | Recurrence design locked: due-date-anchored, completion-only (not cancellation), optional end date/max occurrences, next occurrence reassigned to original_owner_id | Zohaib |
| Session 1 | QA/UX/business-rule pass: fixed Staff-audit-endpoint contradiction (added /api/audit/mine), retry cap (5 attempts→terminal failed), opt-out checked at send-time not just creation-time, opt-out blocks staff+system sends, escalation reassigns to Admin, UTC timestamp storage, reschedule supersedes old queued reminder, concurrent assign returns 409, empty states + real-time draft handling + unread-count trigger defined; documented (not built) multi-worker locking and template char counter as known limitations | Zohaib |
| Session 1 | SMS/task domain pass: template merge fields ({{field}} syntax) + rendered_body snapshot for audit integrity, exponential backoff (1/2/4/8/16 min), STOP keyword variants (STOP/UNSUBSCRIBE/CANCEL/END/QUIT, case-insensitive), task due-date defaults by priority (staff can override), escalated status auto-reverts to in_progress on assignee action | Zohaib |
| Session 1 | Architecture/dev/UX final pass: threads.patient_id unique + find-or-create (prevents duplicate-thread race), default task priority=medium on auto-creation, EF Core auto-migrate on startup + MySQL healthcheck/depends_on for docker-compose reliability, one-time seed step (no re-seed on restart), RFC 7807 ProblemDetails error format, consistent toast notification pattern across screens | Zohaib |
| Build session | Ambiguity flagged by Claude Code during implementation: §6a/§8/§11a contradiction on refresh flow. Resolved: dedicated POST /api/auth/refresh with refresh token rotation (refresh_tokens table added, old token revoked on each use) | Zohaib |
| Build session (step 2) | Ambiguity flagged: BR-011 "5 attempts" vs FR-003's 5 backoff values left one unused. Resolved: 6 total attempts (1 initial + 5 retries), all 5 backoff values used | Zohaib |

*Append new decisions here — do not rewrite history.*

---

## 15. Open questions

None remaining — all resolved as of last session.
