using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class RetryBackoffPolicyTests
{
    // attempt 5 is terminal (see IsTerminal_StopsAtFiveAttempts) — no 6th attempt is ever
    // scheduled, so only the first 4 delays are reachable in practice.
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    public void NextDelay_MatchesExponentialSchedule(int attemptCount, int expectedMinutes)
    {
        var delay = RetryBackoffPolicy.NextDelay(attemptCount);

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), delay);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void IsTerminal_StopsAtFiveAttempts(int attemptCount, bool expectedTerminal)
    {
        Assert.Equal(expectedTerminal, RetryBackoffPolicy.IsTerminal(attemptCount));
    }

    [Fact]
    public void NextDelay_ThrowsForTerminalAttemptCount()
    {
        Assert.Throws<InvalidOperationException>(() => RetryBackoffPolicy.NextDelay(5));
    }
}
