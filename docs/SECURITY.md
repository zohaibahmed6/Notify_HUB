# Security baseline + OWASP Top-10 self-assessment (FR-018)

## Sub-criteria (a)–(e), as required by FR-018

**(a) AuthN/RBAC enforced server-side.** JWT bearer auth (`AuthServiceCollectionExtensions.cs`),
global default-authenticated policy applied to every controller action unless explicitly opted out
(`AuthServiceCollectionExtensions.cs:65-69`) — `[AllowAnonymous]` is used only on
`auth/login`/`refresh`/`logout` (which have their own credential/cookie checks) and the two
shared-secret webhook controllers. Role checks (`[Authorize(Roles = "Admin")]` on `GET /api/audit`,
manual role check on assigning a thread to someone else,
`ThreadsController.cs:190-191`) are enforced in the API layer, never trusted from the client — see
ADR 0003 for the RBAC model itself.

**(b) Input validation on every endpoint, including webhooks.** Data-annotation validation on
request DTOs (ASP.NET Core model binding + `[ApiController]`'s automatic 400 on invalid
`ModelState`); webhook endpoints (`WebhooksController.cs`) require a shared-secret header
(`SharedSecretAttribute.cs:12`, constant-time compare via `CryptographicOperations.
FixedTimeEquals`, :27) in addition to normal payload validation, so a malformed or spoofed
delivery receipt / inbound-reply payload can't reach business logic unauthenticated.

**(c) Parameterized data access only.** All data access goes through EF Core's LINQ provider
(`NotifyHubDbContext`, Fluent API configs) — no raw SQL string concatenation anywhere in the
codebase (verified: no `FromSqlRaw`/`ExecuteSqlRaw` calls with interpolated strings exist in the
repo). Even the 50k-row performance seed (`PerformanceSeedStep.cs`) uses plain EF `AddRange`/
`SaveChangesAsync`, not a bulk-copy raw-SQL shortcut, specifically to keep this guarantee
unconditional (see `CODEBASE_MAP.md` §4a).

**(d) Secrets via environment variables/config, never committed.** `.env` (git-ignored,
`.env.example` committed as the template) supplies `Jwt__Secret`, DB credentials, seeded account
passwords, the webhook shared secret. CI supplies its own throwaway secrets as workflow env vars
(`ci.yml:43-44`), not committed values. No secret literal appears in any `appsettings.json`.

**(e) This document** is the OWASP Top-10 self-assessment.

---

## OWASP Top 10 (2021) self-assessment

| # | Category | Assessment |
|---|---|---|
| A01 | **Broken Access Control** | Server-side role checks on every action (see (a) above); BR-012's thread-assignment race is closed with a real unique-index + 409, not a client-side check; `GET /api/audit/mine` ignores any client-supplied `actor` value and hardcodes it to the caller's own username server-side (`AuditController.cs:33`) specifically so a Staff user can't request another user's audit trail by query-string tampering. **Residual risk:** no per-resource ownership check beyond role — e.g. any authenticated Staff user can view/reply to any thread, not just ones assigned to them. This matches §4's flat "shared inbox" model (any Staff can work any thread) and is a documented design choice, not an oversight. |
| A02 | **Cryptographic Failures** | Passwords hashed via ASP.NET Core Identity's `PasswordHasher<User>` (PBKDF2 with a random per-password salt, `Program.cs:53`) — never stored or logged in plaintext. Refresh tokens are stored hashed (`refresh_tokens.token_hash`, unique index), not as the raw token value, so a DB leak doesn't hand out usable tokens directly. JWTs are signed (HMAC-SHA256, `JwtTokenService.cs:31`) with a secret sourced from config/env, never hardcoded. `UseHttpsRedirection()` is enabled (`Program.cs:123`). **Residual risk:** the mock SMS gateway and shared-secret webhook auth use a plain shared string (not mTLS/HMAC-per-request-signing) — acceptable for a demo mock gateway, would need upgrading for a real carrier integration. |
| A03 | **Injection** | Covered under (c) above — EF Core parameterized queries exclusively. Webhook payloads and all API request bodies are strongly typed DTOs bound by ASP.NET Core's model binder, not manually parsed/concatenated strings. |
| A04 | **Insecure Design** | BR-001's opt-out enforcement is checked twice by design — at message-creation time and again immediately before the gateway call (`MessageDispatcher.cs:42-50`) — specifically so a STOP arriving after a message is already queued still blocks the send, closing a real TOCTOU-shaped design gap rather than trusting a single check. BR-011's retry cap (6 attempts, exponential backoff) prevents a permanently-failing message from retrying forever and exhausting resources. Idempotency keys are a real unique DB constraint (§9), not just an application-level "check then insert" (which would itself be a race) — reinforced by `FindOrCreateThreadAsync`'s optimistic-insert-then-catch pattern for the unique `threads.patient_id` race (`WebhooksController.cs:119-141`, now covered by a real-MySQL concurrency test, `CODEBASE_MAP.md` §5/§7). |
| A05 | **Security Misconfiguration** | CORS restricts to explicitly configured origins only, comma-separated list (`Cors:WebOrigin`), never a wildcard (`Program.cs:125`); Swagger UI is gated behind `app.Environment.IsDevelopment()` (`Program.cs:114-118`) — only reachable because docker-compose sets `ASPNETCORE_ENVIRONMENT=Development` for this assessment build (`docker-compose.yml:28`, so evaluators can reach `/swagger` per FR-017); a real production deployment with `ASPNETCORE_ENVIRONMENT=Production` would have it compiled out of the pipeline entirely, not just hidden by config. Errors return RFC 7807 `ProblemDetails` (`UseExceptionHandler`/`UseStatusCodePages`, `Program.cs:120-121`) rather than leaking stack traces to clients. |
| A06 | **Vulnerable and Outdated Components** | `dotnet restore`/`npm ci` pin exact versions via `.csproj` version ranges and `package-lock.json`; CI runs a dependency-vulnerability scan on every push (`ci.yml`, "Dependency vulnerability scan (.NET)"/"(npm)" steps) — `dotnet list package --vulnerable --include-transitive` fails the build on any advisory-listed package, `npm audit --audit-level=high` likewise. Adding this scan surfaced 4 real High-severity transitive advisories that predated this step (`Microsoft.Extensions.Caching.Memory 8.0.0` GHSA-qj66-m88j-hmgj, `System.Text.Json 8.0.0` GHSA-hh2w-p6rv-4g7w/GHSA-8g4q-xg66-9fp4, `System.Net.Http 4.3.0` GHSA-7jgj-8wvc-jh57, `System.Text.RegularExpressions 4.3.0` GHSA-cmhx-cq75-c4mj) — all resolved by pinning direct `PackageReference`s to patched versions in the affected `.csproj` files; `dotnet list package --vulnerable --include-transitive` now reports zero across the solution. |
| A07 | **Identification and Authentication Failures** | JWT access tokens are short-lived (30 min, §11a) with a rotating refresh token (7-day expiry, old token revoked on every use — `AuthController.IssueTokensAsync`, :117-161) stored in an httpOnly cookie, never readable by JS (§6a) — closing the XSS-exposure tradeoff that an in-memory-only or localStorage-based token would carry. Password policy enforced server-side (min 8 chars, upper/lower/number/symbol, `PasswordPolicy.cs`, 100% unit-tested). Logout actively revokes the refresh token server-side (`AuthController.Logout`, :81), not just a client-side token discard. |
| A08 | **Software and Data Integrity Failures** | CI runs `dotnet build`/`dotnet test`/`npm run build` on every push (FR-014) — a broken build or failing test can't silently merge. Rendered outbound message bodies are snapshotted at send time (`rendered_body`, BR-013) specifically so later template edits can't retroactively alter what audit history shows was actually sent — an integrity guarantee for the audit trail itself. |
| A09 | **Security Logging and Monitoring Failures** | Every one of FR-011's 5 required event types (send, delivery receipt, opt-out, thread assignment, task escalation) is written to `audit_log` with actor + timestamp (confirmed via full-repo grep, `CODEBASE_MAP.md` §5/`AuditController`), plus 2 additional deviations (`blocked`, `superseded`) for send-attempts against opted-out patients and stale reminder supersession — both real security/compliance-relevant events that the literal 5-type list didn't name but that seemed worse to leave silent. `GET /api/audit` (Admin) and `/api/audit/mine` (Staff) make this queryable, not just write-only. **Residual risk:** no centralized log aggregation/alerting (e.g. failed-login-attempt rate alerting) — out of scope for this build, would matter for a real production deployment. |
| A10 | **Server-Side Request Forgery (SSRF)** | The only outbound server-initiated HTTP calls are: the Worker's dispatcher POSTing to the mock gateway, and the mock gateway POSTing back to `api/webhooks/gateway-receipt` via a named, hardcoded `HttpClient` ("self", `MockGatewayController.cs:57-63`) — both fixed, config-known URLs, never built from user-supplied input. There is no endpoint anywhere that fetches a URL supplied by a request body/query string, so classic SSRF (server fetches an attacker-controlled URL) doesn't apply to this codebase's current surface. |

## Summary

No critical/high-severity gaps identified against the actual implemented surface. Automated
dependency-vulnerability scanning is wired into CI (A06) and Swagger UI is environment-gated in
code, reachable only because this assessment build's docker-compose sets `ASPNETCORE_ENVIRONMENT
=Development` (A05) — a real production deployment would need only its own environment variable
changed, no code change, to close both.
