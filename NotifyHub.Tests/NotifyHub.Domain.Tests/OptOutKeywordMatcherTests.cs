using NotifyHub.Domain.Inbox;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class OptOutKeywordMatcherTests
{
    [Theory]
    [InlineData("STOP")]
    [InlineData("stop")]
    [InlineData("Stop")]
    [InlineData("UNSUBSCRIBE")]
    [InlineData("cancel")]
    [InlineData("End")]
    [InlineData("quit")]
    [InlineData("  STOP  ")]
    public void IsOptOutRequest_MatchesKnownVariants(string body)
    {
        Assert.True(OptOutKeywordMatcher.IsOptOutRequest(body));
    }

    [Theory]
    [InlineData("please stop calling me")]
    [InlineData("Can I cancel my appointment?")]
    [InlineData("thanks")]
    [InlineData("")]
    public void IsOptOutRequest_DoesNotMatchOrdinaryReplies(string body)
    {
        Assert.False(OptOutKeywordMatcher.IsOptOutRequest(body));
    }
}
