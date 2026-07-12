# ADR 0003 — RBAC model: two roles (Admin/Staff) vs. a broader role set

**Status:** Accepted (Session 1 — initially 3 roles, reduced to 2; see `PROJECT_CONTEXT.md` §14)

## Context

FR-018(a) requires server-enforced authentication and RBAC. §4's permissions table needs to
distinguish who can do what: view the full audit log vs. only their own actions, manage templates,
work threads/tasks. The initial draft of the requirements considered three roles (Admin, Staff,
Reporting — a read-only, cross-cutting "view everything, change nothing" role for compliance/
oversight use cases).

## Decision

**Two roles only: `Admin` and `Staff`** (`NotifyHub.Domain/Enums/UserRole.cs:3`). Enforced via
ASP.NET Core's standard `[Authorize]`/`[Authorize(Roles = "Admin")]` attributes and a global
default-authenticated policy (`AuthServiceCollectionExtensions.cs:65-69` — any authenticated user,
Admin or Staff, passes unless a specific action opts into the stricter Admin-only policy, which
today is exactly one endpoint: `GET /api/audit`, `AuditController.cs:22`). No separate
`Reporting`/read-only role exists.

Permissions actually implemented (`PROJECT_CONTEXT.md` §4):

| Role | Can do |
|---|---|
| Admin | Everything Staff can, plus: view the full audit log across all actors (`GET /api/audit`), assign threads to any staff member (not just self) |
| Staff | Work assigned threads, reply, create/complete tasks, manage templates, view only their own audit trail (`GET /api/audit/mine`) |

## Rejected alternatives

- **Three roles (Admin/Staff/Reporting), the original draft.** Rejected during Session 1's
  requirements pass and re-confirmed here against the actual build: everywhere the spec calls for a
  permission distinction (§4's table, the audit log's Admin-vs-own-actions split in §8), it reduces
  to exactly two buckets — "can see/do everything" and "can see/do their own slice." A third
  read-only role would duplicate Admin's *visibility* without adding a *distinct* permission
  boundary anywhere in the actual FR/BR list — there's no requirement anywhere ("Reporting can see
  X but not Y that Admin can") that a third role would uniquely satisfy. Adding it would mean
  building and testing a third set of authorization branches (and a third set of Swagger/RBAC
  documentation, FR-018/FR-017) purely speculatively, with no acceptance criteria driving its
  actual shape — exactly the kind of unrequested abstraction this build tries to avoid under a
  3-day deadline.
- **A finer-grained per-action permission model** (e.g. a `permissions` table, policy-based
  authorization with named claims like `audit:read:all`, `templates:write`) instead of two fixed
  roles. Rejected: nothing in the spec calls for permissions to vary independently of role — no
  case exists where two Staff users need different capabilities from each other, or where an
  Admin's capabilities need to be selectively restricted. A claims/permissions table is the right
  design once that requirement appears; building it pre-emptively for a 2-role system adds a
  `permissions` table, seed data, and an extra layer of indirection with no corresponding
  requirement to justify it — same overengineering concern as the rejected third role, one level
  more abstract.
- **No roles at all / single authenticated-user model** (every logged-in user can do everything).
  Rejected outright: FR-018(a) explicitly requires RBAC, and §4 explicitly distinguishes Admin from
  Staff (e.g. "Cannot manage users" isn't relevant pre-user-management, but "view own audit trail"
  for Staff vs. full audit for Admin is a real, tested boundary — `AuditControllerTests` asserts
  Staff gets a 403 from `GET /api/audit`).

## Consequences

- **Positive:** every authorization check in the codebase is a plain role check
  (`[Authorize(Roles = "Admin")]` or the default any-authenticated policy) — no policy/claims
  infrastructure to build, test, or explain in the code walkthrough (§13); RBAC enforcement is
  server-side everywhere per BR-005 and is trivially testable (role-branch tests exist for the two
  places it actually matters: `GET /api/audit` and assigning a thread to someone other than
  yourself, `ThreadsController.cs:190-191`).
- **Negative, explicitly out of scope:** no user-management screen exists (§4: "no dedicated
  user-management UI — decision: only must-have screens built"), so roles are seed-assigned only;
  if a third role or per-permission model is needed later, it would need both a schema change
  (`UserRole` enum → a proper permissions model) and a UI to manage it — flagged here as the actual
  cost of changing this decision later, not a hidden one.
