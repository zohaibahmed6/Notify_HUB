using NotifyHub.Domain.Messaging;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class RateLimitCheckerTests
{
    [Theory]
    [InlineData(0, 20, true)]
    [InlineData(19, 20, true)]
    [InlineData(20, 20, false)]
    [InlineData(21, 20, false)]
    public void IsAllowed_ComparesAgainstMax(int recentCount, int max, bool expected)
    {
        Assert.Equal(expected, RateLimitChecker.IsAllowed(recentCount, max));
    }
}
