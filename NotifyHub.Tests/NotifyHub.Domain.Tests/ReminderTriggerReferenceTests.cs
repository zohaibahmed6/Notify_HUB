using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class ReminderTriggerReferenceTests
{
    private static readonly DateTime ScheduledAt = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Build_RoundTrips_ThroughTryParse()
    {
        var reference = ReminderTriggerReference.Build(appointmentId: 42, offsetHours: 48, ScheduledAt);

        var parsed = ReminderTriggerReference.TryParse(reference, out var appointmentId, out var offsetHours, out var ticks);

        Assert.True(parsed);
        Assert.Equal(42, appointmentId);
        Assert.Equal(48, offsetHours);
        Assert.Equal(ScheduledAt.Ticks, ticks);
    }

    [Fact]
    public void Build_DiffersWhenScheduledAtChanges()
    {
        var original = ReminderTriggerReference.Build(42, 48, ScheduledAt);
        var rescheduled = ReminderTriggerReference.Build(42, 48, ScheduledAt.AddDays(1));

        Assert.NotEqual(original, rescheduled);
    }

    [Theory]
    [InlineData("appointment:42:created")] // pre-scheduler seed-data format (DemoOutboundMessageSeedStep)
    [InlineData("medication:7:seed")]
    [InlineData("appointment:not-a-number:reminder:48h:12345")]
    [InlineData("appointment:42:reminder:48x:12345")] // missing trailing 'h'
    [InlineData("")]
    public void TryParse_False_ForNonReminderOrMalformedReferences(string reference)
    {
        var parsed = ReminderTriggerReference.TryParse(reference, out _, out _, out _);

        Assert.False(parsed);
    }
}
