using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class RetryBackoffPolicyTests
{
    // 6 total attempts (1 initial send + 5 retries) — all 5 backoff values are reachable,
    // used between each pair of attempts.
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void NextDelay_MatchesExponentialSchedule(int attemptCount, int expectedMinutes)
    {
        var delay = RetryBackoffPolicy.NextDelay(attemptCount);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), delay);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(5, false)]
    [InlineData(6, true)]
    [InlineData(7, true)]
    public void IsTerminal_StopsAtSixAttempts(int attemptCount, bool expectedTerminal)
    {
        Assert.Equal(expectedTerminal, RetryBackoffPolicy.IsTerminal(attemptCount));
    }

    [Fact]
    public void NextDelay_ThrowsForTerminalAttemptCount()
    {
        Assert.Throws<InvalidOperationException>(() => RetryBackoffPolicy.NextDelay(6));
    }
}
