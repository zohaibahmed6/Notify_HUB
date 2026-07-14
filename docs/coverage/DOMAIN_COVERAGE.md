# FR-013 — Domain test coverage

**Result: 94.2% line coverage, 97.7% branch coverage, 92.1% method coverage on `NotifyHub.Domain`** — comfortably above the required 70%.

> **Note (Step 9):** this is the step-7 measurement, kept as the FR-013 record — it has not been
> re-run since. The Domain project has changed since: `ReminderDueCalculator`/
> `ReminderTriggerReference` (both listed at 100% below) were deleted in Step 9's P9-08, and new
> pure calculators were added across Steps 8–9 (`RateLimitChecker`, `QuietHoursCalculator`,
> `MessageExpiryCalculator`, `ReminderScheduleCalculator`, `PduSegmentCalculator`,
> `TaskForwardingRuleOverlap`), each with its own dedicated unit-test file in
> `NotifyHub.Domain.Tests`. Re-run the commands below for a current number.

## How this number was produced

Coverage is collected via `coverlet.collector` (already referenced by both test projects) and
merged/summarized with `dotnet-reportgenerator-globaltool`, filtered to just the `NotifyHub.Domain`
assembly:

```
dotnet test NotifyHub.sln -c Release --filter "Category!=MySql" --collect:"XPlat Code Coverage" --results-directory ./coverage-raw

dotnet tool install --global dotnet-reportgenerator-globaltool   # one-time
reportgenerator -reports:"./coverage-raw/*/coverage.cobertura.xml" -targetdir:"./coverage-report" -reporttypes:Html;TextSummary -classfilters:"+NotifyHub.Domain.*"
```

The MySQL-only race test (`InboundWebhookThreadRaceMySqlTests`, `[Trait("Category","MySql")]`) is
excluded because it needs a real MySQL container and doesn't touch `NotifyHub.Domain` — it's a
persistence-layer race test, not a domain-logic test.

Both `NotifyHub.Domain.Tests` (pure unit tests) and `NotifyHub.Integration.Tests` (API-level tests
that construct and exercise Domain entities through EF Core) are included in the same coverage run
and merged. This is deliberate, not a loophole: the Domain project contains two different kinds of
code —

1. **Pure business-rule logic** (`RetryBackoffPolicy`, `RecurrenceCalculator`,
   `OptOutKeywordMatcher`, `TaskDueDateDefaults`, `ReminderDueCalculator`,
   `ReminderTriggerReference`, `IdempotencyKeyGenerator`, `TemplateRenderer`, `PasswordPolicy`) —
   these are exercised directly by `NotifyHub.Domain.Tests`, all at or near 100%.
2. **Entity classes** (`Patient`, `ConversationThread`, `OutboundMessage`, `TaskItem`, etc.) — plain
   EF-mapped POCOs with no logic of their own beyond auto-properties. These are never instantiated
   by the pure unit tests (there's nothing to unit-test on a property bag), but they're constructed
   and mutated constantly by the integration test suite, which is the realistic way this code is
   actually exercised end-to-end.

Running `NotifyHub.Domain.Tests` in isolation understates real coverage (56.3% — the entity classes
read as untouched, even though every integration test creates dozens of them) and running only
integration tests would overstate it in the other direction by skipping the pure-logic unit tests
entirely. The combined run is the accurate picture of what's actually covered.

## Per-class breakdown (2026-07-12)

| Class | Line coverage |
|---|---|
| `RetryBackoffPolicy`, `RecurrenceCalculator`, `OptOutKeywordMatcher`, `IdempotencyKeyGenerator`, `ReminderDueCalculator`, `ReminderTriggerReference`, `TemplateRenderer`, `PasswordPolicy` | 100% |
| `AuditLog`, `ConversationThread`, `MessageTemplate`, `Patient`, `RefreshToken`, `User` | 100% |
| `OutboundMessage` | 93.7% |
| `TaskItem` | 86.6% |
| `TaskDueDateDefaults` | 87.5% |
| `Appointment` | 80% |
| `DeliveryStatusHistory`, `InboundMessage` | 60% (uncovered lines are unused property setters — e.g. `InboundMessage.Id` is only ever set by EF Core's own materialization, not application code, so no test path hits the setter directly) |

Overall: **147 of 156 coverable lines**, 20 classes, 1 assembly.

## Reproducing this report

Not committed as a build artifact (HTML coverage reports are large and go stale immediately) —
regenerate on demand with the commands above, or run just the summary step against CI's own
`coverage.cobertura.xml` artifact.
