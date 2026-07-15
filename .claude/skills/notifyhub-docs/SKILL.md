---
name: notifyhub-docs
description: Maintains NotifyHub's documentation-driven workflow (docs/DOCUMENT_INDEX.md manifest, docs/DECISIONS.md log, per-module doc files, completion checklist) as required by the root CLAUDE.md. Use before starting any feature work (plan-first, module-isolated, no assumed business rules) and after any change that adds/changes entities, endpoints, jobs, business logic, or tests. Also use when asked to "update the docs", "sync documentation", "log this decision", "check the doc index", or "run the completion checklist".
---

# NotifyHub documentation workflow

This repo's root `CLAUDE.md` mandates a documentation-driven workflow. This skill is the
concrete procedure for that mandate — it doesn't restate the policy, it executes it against
this repo's actual files.

## Repo doc map (read, don't duplicate)

| File | Role |
|---|---|
| `docs/DOCUMENT_INDEX.md` | **The manifest.** One tree entry per feature (see format below), cross-referencing everything below. Fully backfilled: Inbox, Tasks, SMS, Users, Templates (incl. Bookmarks), Settings, Audit Log, Dashboard, Auth. |
| `docs/Frontend/<Feature>.md`, `docs/Backend/<Feature>.md`, `docs/Worker/<Job>.md` | Thin anchor files the manifest points to. Each just cross-references `CODEBASE_MAP.md`/`PROJECT_CONTEXT.md` — no duplicated prose. Create one the first time a feature is added to the manifest; only add real content when there's something to say that `CODEBASE_MAP.md` doesn't already cover. |
| `docs/DECISIONS.md` | **The decision log.** Chronological, one entry per architectural/technical/business decision. Create it if missing (schema below). |
| `CODEBASE_MAP.md` | Reality-first implementation map (file:line citations). Already required to be updated same-change by root `CLAUDE.md` — manifest entries point here for implementation detail, never repeat it. |
| `PROJECT_CONTEXT.md` | Spec — FR-/BR- numbered requirements and business rules. Authoritative for "Business Rules" and "Functional Specification" priority tiers. |
| `STATUS.md` | Build log — step/increment history, deviations, known limitations, checklists per increment. |
| `docs/adr/NNNN-*.md` | Formal Architecture Decision Records for major structural decisions (queueing, hosting model, RBAC, etc.). `docs/DECISIONS.md` does **not** replace these — link to the ADR file for anything that already warrants one; use `DECISIONS.md` for everything else. |
| `docs/SECURITY.md` | General OWASP/security baseline — not per-feature sectioned; manifest entries link to it plus the relevant `CODEBASE_MAP.md` §3 auth notes. |
| `docs/coverage/DOMAIN_COVERAGE.md` | Test coverage report. |

## Planning workflow (before any implementation)

1. Read only the relevant documentation — start at `docs/DOCUMENT_INDEX.md`, follow its links
   for the touched feature(s) only. Don't re-scan the whole codebase (see Module isolation).
2. Produce an implementation plan.
3. Wait for explicit user approval before writing any code.
4. If something is unclear, ask only functional clarification questions (see Functional
   requirements below) — not implementation-mechanics questions the plan should already answer.
5. Never assume business rules — if `PROJECT_CONTEXT.md`/the manifest don't cover it, ask.
6. Never begin coding before approval.

If two sources conflict while researching, resolve using this priority order and **report the
conflict to the user before implementing** rather than guessing:
1. Approved Business Rules (`PROJECT_CONTEXT.md` BR- entries, or explicit user instruction)
2. Functional Specification (`PROJECT_CONTEXT.md` FR- entries)
3. Feature documentation (`docs/DOCUMENT_INDEX.md` entry + its linked anchor files)
4. Architecture documentation (`CODEBASE_MAP.md`)
5. Source code
6. Comments

## Module isolation

- Read only the affected feature's manifest entry and whatever it links to.
- Read dependent modules only if the change actually crosses into them (e.g. a Tasks change
  that also touches escalation-triggered forwarding, or a Reminder SMS change that touches
  Inbox's composer).
- Never scan the entire repository when `docs/DOCUMENT_INDEX.md` already identifies the
  relevant modules — that's the entire point of maintaining it.

## Implementation rules

Never ask the user to run bash/PowerShell commands, SQL scripts, project builds, Docker,
migrations, or tests **unless they explicitly requested that specific action in this task**.
Assume implementation — including running the app, applying migrations, and running tests to
verify — is performed directly, not handed back to the user as a manual step.

This doesn't conflict with the top-level `CLAUDE.md`'s "start the dev server and use the
feature in a browser before reporting complete" guidance — doing that yourself *is* the
compliant action. The rule here is about not asking the user to do it instead of you.

## Functional requirements

- If functional requirements are ambiguous, ask concise functional questions and wait for
  clarification before implementing.
- Never infer or invent business rules — if it's not in `PROJECT_CONTEXT.md`, the manifest, or
  explicit user instruction, ask rather than guess (same priority order as above).

## Documentation rules (after implementation)

Run this whenever a change added/changed an entity, endpoint, job, business rule, or test —
same commit/session as the change, not a follow-up task.

1. **Update only affected documentation.** Don't touch unrelated manifest entries or files.
2. **Manifest** — add or update the feature's tree entry in `docs/DOCUMENT_INDEX.md` when a
   module is added, or when an existing feature's fields (Frontend/Backend/Worker/Database/
   Security/Business Rules/APIs/Source folders/Related/Dependencies) changed. Format:
   ```
   FeatureName
    ├── Frontend        → docs/Frontend/FeatureName.md
    ├── Backend         → docs/Backend/FeatureName.md
    ├── Worker          → docs/Worker/JobName.md  (or "none" + why)
    ├── Database        → entities — CODEBASE_MAP.md §2
    ├── Security        → auth policy notes — CODEBASE_MAP.md §3, docs/SECURITY.md
    ├── Business Rules  → FR-/BR- IDs
    ├── APIs            → controllers/routes — CODEBASE_MAP.md §3
    ├── Source folders  → real paths, not invented ones
    ├── Related         → other manifest features this one touches
    └── Dependencies    → other features/services this one requires
   ```
   Create the linked `docs/Frontend/`, `docs/Backend/`, `docs/Worker/` anchor files if they
   don't exist yet (see existing ones for the thin-pointer style — cross-reference
   `CODEBASE_MAP.md`, don't restate it).
3. **Decision log** — if the change involved a real decision (not just "implemented what was
   asked"), append an entry to `docs/DECISIONS.md`:
   ```
   ## YYYY-MM-DD — <decision title>
   **Decision:** ...
   **Reason:** ...
   **Alternatives considered:** ...
   **Impacted modules:** ...
   ```
   If the decision is architecturally significant enough to warrant a full ADR, write
   `docs/adr/NNNN-title.md` (next number, follow the format of `0001`–`0003`) instead/as well,
   and link it from the `DECISIONS.md` entry rather than duplicating its content.
4. **Preserve existing documentation** — extend/merge, never rewrite wholesale.
5. **Never duplicate content** — cross-reference `CODEBASE_MAP.md`/`PROJECT_CONTEXT.md`/ADRs
   instead of restating them in the manifest or anchor files.
6. **Cross-reference check** — confirm `CODEBASE_MAP.md` was already updated (root `CLAUDE.md`
   requires this same-change); confirm links between `DOCUMENT_INDEX.md` ↔ its anchor files ↔
   `CODEBASE_MAP.md` ↔ `PROJECT_CONTEXT.md` ↔ ADRs resolve to real sections, not stale ones.
7. **If the manifest example/format ever conflicts with what the code actually does** (as
   happened with the Tasks/SMS worker entries — see `docs/DOCUMENT_INDEX.md`'s flagged notes),
   trust the code, fix the manifest, and flag the discrepancy rather than silently matching a
   stale template.

## Completion checklist

Before reporting a task done, verify and state explicitly which of these hold (call out any
that don't, don't silently skip):
- [ ] Implementation matches approved requirements
- [ ] `CODEBASE_MAP.md` updated (relevant section only)
- [ ] `docs/DOCUMENT_INDEX.md` entry added/updated (+ anchor files if new)
- [ ] `docs/DECISIONS.md` entry added (if a decision was made) / ADR added (if warranted)
- [ ] Cross-references valid
- [ ] Business rules documented (FR-/BR- IDs referenced where relevant)
- [ ] APIs documented if changed
- [ ] Config changes documented
- [ ] Known limitations updated (`STATUS.md`, if applicable)

## Adding a new feature to the manifest

`docs/DOCUMENT_INDEX.md` is fully backfilled: Inbox, Tasks, SMS, Users, Templates (incl.
Bookmarks), Settings, Audit Log, Dashboard, Auth. When a genuinely new feature is added later:
1. Add its tree entry to `docs/DOCUMENT_INDEX.md` following the format above.
2. Create its `docs/Frontend/<Feature>.md` / `docs/Backend/<Feature>.md` / `docs/Worker/<Job>.md`
   anchor files (thin pointers, matching the existing ones — cross-reference `CODEBASE_MAP.md`,
   don't restate it).
3. Ground every field in the actual code (`CODEBASE_MAP.md`/source), not in a template or
   assumption — if the obvious-sounding structure doesn't match reality (as happened with the
   Tasks/SMS worker entries), trust the code and flag the mismatch.

`docs/DECISIONS.md` doesn't exist yet — create it on first use, starting the log from the
current date forward. Don't reconstruct historical decisions from git log; `STATUS.md` and the
existing ADRs already cover project history.
