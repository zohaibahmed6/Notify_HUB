using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class IdempotencyKeyGeneratorTests
{
    [Fact]
    public void Generate_IsDeterministic()
    {
        var first = IdempotencyKeyGenerator.Generate(1, 2, "appointment:5:created");
        var second = IdempotencyKeyGenerator.Generate(1, 2, "appointment:5:created");

        Assert.Equal(first, second);
    }

    [Fact]
    public void Generate_DiffersByTriggerReference()
    {
        // BR-009: a reschedule produces a new trigger_reference, so it must not collide
        // with the original — otherwise a legitimate reschedule reminder would be
        // blocked as a duplicate.
        var created = IdempotencyKeyGenerator.Generate(1, 2, "appointment:5:created");
        var rescheduled = IdempotencyKeyGenerator.Generate(1, 2, "appointment:5:rescheduled:1");

        Assert.NotEqual(created, rescheduled);
    }

    [Fact]
    public void Generate_DiffersByPatientOrTemplate()
    {
        var baseline = IdempotencyKeyGenerator.Generate(1, 2, "appointment:5:created");

        Assert.NotEqual(baseline, IdempotencyKeyGenerator.Generate(9, 2, "appointment:5:created"));
        Assert.NotEqual(baseline, IdempotencyKeyGenerator.Generate(1, 9, "appointment:5:created"));
    }

    [Fact]
    public void Generate_ReturnsLowercaseHex()
    {
        var key = IdempotencyKeyGenerator.Generate(1, 2, "appointment:5:created");

        Assert.Equal(64, key.Length);
        Assert.Matches("^[0-9a-f]{64}$", key);
    }

    // P9-08 rule 30: reminder key uses a separate hash input from Generate above.
    [Fact]
    public void GenerateForReminder_IsDeterministic()
    {
        var eventTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

        var first = IdempotencyKeyGenerator.GenerateForReminder(1, 2, eventTime, 1440);
        var second = IdempotencyKeyGenerator.GenerateForReminder(1, 2, eventTime, 1440);

        Assert.Equal(first, second);
    }

    [Fact]
    public void GenerateForReminder_DiffersByEventTimeOrOffset()
    {
        var eventTime = new DateTime(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);
        var baseline = IdempotencyKeyGenerator.GenerateForReminder(1, 2, eventTime, 1440);

        Assert.NotEqual(baseline, IdempotencyKeyGenerator.GenerateForReminder(1, 2, eventTime.AddMinutes(1), 1440));
        Assert.NotEqual(baseline, IdempotencyKeyGenerator.GenerateForReminder(1, 2, eventTime, 60));
    }
}
