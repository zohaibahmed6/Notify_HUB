using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class ReminderDueCalculatorTests
{
    private static readonly DateTime ScheduledAt = new(2026, 7, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void IsDue_False_BeforeWindowOpens()
    {
        var now = ScheduledAt.AddHours(-49); // 1 hour before the 48h window opens

        Assert.False(ReminderDueCalculator.IsDue(ScheduledAt, offsetHours: 48, now));
    }

    [Fact]
    public void IsDue_True_ExactlyAtWindowOpen()
    {
        var now = ScheduledAt.AddHours(-48);

        Assert.True(ReminderDueCalculator.IsDue(ScheduledAt, offsetHours: 48, now));
    }

    [Fact]
    public void IsDue_True_WithinWindow()
    {
        var now = ScheduledAt.AddHours(-10);

        Assert.True(ReminderDueCalculator.IsDue(ScheduledAt, offsetHours: 48, now));
    }

    [Fact]
    public void IsDue_True_ImmediatelyForAppointmentBookedInsideTheWindow()
    {
        // Appointment scheduled only 24h out — already inside the 48h reminder window
        // at the moment it's created, so the reminder should fire right away rather
        // than being skipped because the window "already started".
        var now = ScheduledAt.AddHours(-24);

        Assert.True(ReminderDueCalculator.IsDue(ScheduledAt, offsetHours: 48, now));
    }

    [Fact]
    public void IsDue_False_AtOrAfterTheAppointmentItself()
    {
        Assert.False(ReminderDueCalculator.IsDue(ScheduledAt, offsetHours: 48, now: ScheduledAt));
        Assert.False(ReminderDueCalculator.IsDue(ScheduledAt, offsetHours: 48, now: ScheduledAt.AddMinutes(1)));
    }

    [Theory]
    [InlineData(48)]
    [InlineData(2)]
    public void IsDue_True_ExactlyAtWindowOpen_ForEitherOffset(int offsetHours)
    {
        var now = ScheduledAt.AddHours(-offsetHours);

        Assert.True(ReminderDueCalculator.IsDue(ScheduledAt, offsetHours, now));
    }
}
