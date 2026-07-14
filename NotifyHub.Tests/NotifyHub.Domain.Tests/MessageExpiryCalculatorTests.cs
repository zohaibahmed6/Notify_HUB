using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class MessageExpiryCalculatorTests
{
    [Fact]
    public void CalculateExpiresAt_ImmediateSend_IsCreatedAtPlus12Hours()
    {
        var createdAt = new DateTime(2026, 7, 14, 10, 0, 0, DateTimeKind.Utc);

        var expiresAt = MessageExpiryCalculator.CalculateExpiresAt(createdAt, scheduledAt: null);

        Assert.Equal(createdAt + TimeSpan.FromHours(12), expiresAt);
    }

    [Fact]
    public void CalculateExpiresAt_ScheduledSend_IsScheduledAtPlus12Hours_NotCreatedAt()
    {
        var createdAt = new DateTime(2026, 7, 14, 10, 0, 0, DateTimeKind.Utc);
        var scheduledAt = new DateTime(2026, 7, 16, 9, 0, 0, DateTimeKind.Utc);

        var expiresAt = MessageExpiryCalculator.CalculateExpiresAt(createdAt, scheduledAt);

        Assert.Equal(scheduledAt + TimeSpan.FromHours(12), expiresAt);
    }
}
