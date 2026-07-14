using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class ReminderScheduleCalculatorTests
{
    private static readonly DateTime EventTime = new(2026, 8, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CalculateScheduledSendTime_IsEventTimeMinusOffset()
    {
        var scheduledSendTime = ReminderScheduleCalculator.CalculateScheduledSendTime(EventTime, reminderOffsetMinutes: 1440);

        Assert.Equal(EventTime.AddDays(-1), scheduledSendTime);
    }

    [Fact]
    public void CalculateExpiryTime_IsEventTimeMinusExpiryOffset_NotScheduledSendTime()
    {
        // Rules 17/18: expiry is anchored to Event Time directly, never to Scheduled Send
        // Time or Created Time.
        var expiryTime = ReminderScheduleCalculator.CalculateExpiryTime(EventTime, reminderExpiryOffsetMinutes: 15);

        Assert.Equal(EventTime.AddMinutes(-15), expiryTime);
    }

    [Fact]
    public void MinSelectableEventTime_IsNowPlusOffset()
    {
        var now = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

        var min = ReminderScheduleCalculator.MinSelectableEventTime(now, reminderOffsetMinutes: 1440);

        Assert.Equal(now.AddDays(1), min);
    }

    [Fact]
    public void EventTime_AtMinSelectable_ProducesScheduledSendTimeOfExactlyNow()
    {
        // Rule 9's own definition: the minimum selectable Event Time is precisely the one
        // whose computed Scheduled Send Time equals "now".
        var now = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);
        var minEventTime = ReminderScheduleCalculator.MinSelectableEventTime(now, 1440);

        var scheduledSendTime = ReminderScheduleCalculator.CalculateScheduledSendTime(minEventTime, 1440);

        Assert.Equal(now, scheduledSendTime);
    }
}
