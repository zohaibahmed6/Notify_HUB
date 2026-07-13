# AI Usage Log (FR-019)

**Declared tools:** Claude (Claude Code, CLI agent) — used for effectively the entire build,
session-by-session, as detailed below. ChatGPT was the other declared tool (`PROJECT_CONTEXT.md`
header) for early requirements/spec drafting before the build sessions began.

> **Note on this section, left deliberately visible rather than silently filled in:** everything
> below is written from Claude Code's own record of the build sessions (this tool has no visibility
> into any separate ChatGPT conversation used during the earlier requirements-gathering phase that
> produced the original assessment-pack addendum). If a ChatGPT session was used for that phase,
> add a short paragraph here naming which parts of §1–§3/§14's "Session 1" decisions came from it —
> Claude Code cannot honestly reconstruct a conversation it wasn't part of.

## Phases covered

| Phase | How AI was used |
|---|---|
| **Design** | `PROJECT_CONTEXT.md` itself — locking scope, data model, API surface, business rules — was built and refined turn-by-turn in chat before any code was written (§14's "Session 1" change-log rows), including resolving real spec contradictions (e.g. the 4-value `tasks.status` enum having no state for BR-007b's "cancel ends the series"). Every subsequent build session re-derived its plan against this document rather than against ad-hoc requests. |
| **Scaffold** | Solution structure (`Api`/`Worker`/`Domain`/`Infrastructure`/`Tests` split), EF Core model + migrations, Docker/compose, CI pipeline skeleton — all generated directly by Claude Code, then iterated against `dotnet build`/`dotnet test` output in the same session. |
| **Implement** | Every FR from FR-001 through FR-011, plus the two remaining screens and the audit/seed work in step 6, and this documentation step (step 7) — implemented directly by Claude Code across the numbered build steps in `STATUS.md`. |
| **Test** | Domain unit tests and integration tests were written alongside (not after) each feature, in the same session as the implementation — e.g. `ReminderDueCalculatorTests`/`ReminderTriggerReferenceTests` for step 5, `AuditControllerTests`/`PerformanceSeedStepTests` for step 6. A real-MySQL-only race test (`InboundWebhookThreadRaceMySqlTests`) was added specifically because EF Core's InMemory provider can't reproduce genuine connection-level locking, after Claude Code identified that gap unprompted. |
| **Debug** | Live Docker walkthroughs surfaced real bugs that were diagnosed and fixed in-session — see the "AI was wrong" example below for one, and `STATUS.md`'s "Step 4 bug-fix + e2e round" for three more (empty-200-body toast misread, page-refresh forced re-login, escalated-task revert never firing because no UI existed to trigger it). |
| **Docs** | This file, the three ADRs, the OWASP self-assessment, `CODEBASE_MAP.md`, and `STATUS.md` itself are all Claude Code output, kept incrementally updated alongside code changes rather than written once at the end (per this repo's own `CLAUDE.md` standing instruction). |

## Three representative sessions

### 1. Frontend — shared inbox screen + real-time updates (step 4)

Built `InboxPage`/`ConversationPanel`/`useInboxHub` end-to-end in one session: thread list,
merged inbound/outbound conversation view (BR-008), reply/assign/make-task actions, and a SignalR
client wired to `/hubs/inbox`. The trickiest part wasn't the SignalR wiring itself but §6c's
UX rule that a new real-time message must append below an open draft *without touching the draft*,
and auto-scroll *only if the view was already at the bottom* — this required tracking "were we
scrolled to the bottom before this update" as separate state from the message list itself
(`wasAtBottomRef` in `ConversationPanel.tsx`), which Claude Code got right on the first pass because
the constraint was stated precisely in the spec rather than inferred.

### 2. Backend/Domain — outbound retry/backoff pipeline (step 3)

FR-003's idempotent-dispatch-with-backoff requirement and BR-011's retry cap were implemented as a
pure, unit-tested `RetryBackoffPolicy` class (delays 1/2/4/8/16 min, terminal after 6 attempts) kept
completely separate from the EF Core/HTTP dispatcher logic that calls it — deliberately, so the
retry math could be tested with zero database or network dependency. Mid-session, a genuine spec
ambiguity was caught and flagged rather than guessed past: BR-011 said "5 attempts" but FR-003 listed
5 distinct backoff values, which is only consistent if "5 attempts" means 5 *retries* on top of the
initial send (6 total), not 5 attempts total with one backoff value going unused. Flagged to Zohaib,
confirmed, `RetryBackoffPolicy.MaxAttempts` set to 6, tests updated to match (`PROJECT_CONTEXT.md`
§14, "Build session (step 2)" row).

### 3. Backend + docs — audit log, 50k-message seed, and this documentation step (steps 6–7)

Step 6 began with an explicit instruction to check what already existed before building anything —
Claude Code grepped every `AuditLogger.Add` call site first and reported back that all 5 of FR-011's
required audit event types were already wired by earlier steps, avoiding redundant rework, then
built only the genuine gaps (the `/api/audit` endpoints, the Templates edit endpoint, the 50k seed).
Step 7 (this step) is the AI-tool-log/ADR/OWASP-doc work itself — an example of AI-assisted work
that isn't code generation at all (see the dedicated example below), including running the actual
test suite with code coverage collection and a coverage-report tool to get a real, defensible
94.2%-line-coverage number for FR-013 rather than an estimated one.

## "AI was wrong / unsafe" example, and the fix

During step 6, Claude Code found that `ThreadsController.Detail` loaded a thread's **entire**
inbound+outbound message history unpaginated on every request (`.Include()` with no limit). This
was logged as a "found, not fixed — out of scope for this step" item in the first pass of `STATUS
.md`'s Final review checklist, reasoning that FR-010's pagination requirement was really about the
*inbox list*, not an individual thread's detail view.

**That reasoning was wrong**, and Zohaib caught it: FR-010's acceptance criteria says "thread views
must stay fast, paginated" in the affirmative, and a thread accumulating thousands of messages
(entirely plausible for a real patient over time, and directly provoked by the very 50k-message
seed being built in the same step) would make this a real, user-visible performance bug, not a
hypothetical one — deferring it was scope-narrowing that hadn't actually been asked for. Claude Code
had treated "the literal work-item list" as the ceiling of the step rather than the floor, which is
the unsafe part: silently downgrading a real requirement violation to a "nice to have" without
flagging it as a judgment call first.

**Fix:** `ThreadsController.Detail`'s messages were rewritten to a genuine merge-pagination
(`GetMessagesPageAsync`, `ThreadsController.cs:89-132`) that pulls only `skip+pageSize` rows from
each of `inbound_messages`/`outbound_messages` — provably sufficient to answer any page correctly,
documented in the method itself — plus a test proving it doesn't load the full history
(`ThreadsControllerTests.Detail_PaginatesMessages_DoesNotReturnFullHistory`) and a frontend
"Load earlier messages" affordance so history beyond page 1 stayed reachable. The lesson carried
into every session since: when a build step's literal instructions and a requirement's plain-English
acceptance criteria disagree, flag it explicitly rather than silently resolving in favor of the
narrower reading.

## AI used beyond code generation

Two distinct examples, from different points in the build:

1. **Root-cause diagnosis of a dependency conflict, not a code-writing task.** Between build steps,
   `npm ci` started failing with an `ERESOLVE` peer-dependency error between the pinned `vite@^8.1.4`
   and an older `@vitejs/plugin-react@^4.3.2`. Claude Code diagnosed this was a pre-existing
   inconsistency (not introduced by the session's own changes — confirmed by checking installed
   versions vs. the lockfile), checked the npm registry directly for a compatible plugin version,
   applied the fix, and then *verified* it with a from-scratch `npm ci` + `npm run build` + `npm run
   dev` cycle rather than assuming the version bump alone was sufficient. No source code was
   written or changed in this task at all.
2. **This documentation step itself.** The three ADRs, the OWASP self-assessment, and the FR-013
   coverage number in this log were all produced by having Claude Code read the actual
   implementation and its own build history, then write structured judgment/analysis documents
   against it (e.g. actually running `dotnet test --collect:"XPlat Code Coverage"` +
   `reportgenerator` to get a real 94.2% figure instead of estimating one) — using the tool for
   analysis and technical writing, not generating application code.

## Tooling note

All work logged here was performed via Claude Code (terminal-based agentic coding tool), operating
directly against this repository's files, `dotnet`/`npm` toolchains, and a live Docker environment
across multiple sessions. No other AI coding tool touched this repository during the build phase.
