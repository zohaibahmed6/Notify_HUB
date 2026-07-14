using NotifyHub.Domain.Tasks;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class TaskForwardingRuleOverlapTests
{
    private static readonly DateTime D1 = new(2026, 1, 1);
    private static readonly DateTime D2 = new(2026, 2, 1);
    private static readonly DateTime D3 = new(2026, 3, 1);
    private static readonly DateTime D4 = new(2026, 4, 1);

    [Fact]
    public void RangesOverlap_IdenticalRanges_Overlap()
    {
        Assert.True(TaskForwardingRuleOverlap.RangesOverlap(D1, D2, D1, D2));
    }

    [Fact]
    public void RangesOverlap_DisjointRanges_DoNotOverlap()
    {
        Assert.False(TaskForwardingRuleOverlap.RangesOverlap(D1, D2, D3, D4));
    }

    [Fact]
    public void RangesOverlap_AdjacentButNotTouching_DoNotOverlap()
    {
        Assert.False(TaskForwardingRuleOverlap.RangesOverlap(D1, D2, D2.AddDays(1), D3));
    }

    [Fact]
    public void RangesOverlap_TouchingAtBoundary_DoOverlap()
    {
        // Inclusive bounds — a rule ending exactly when another starts still overlaps.
        Assert.True(TaskForwardingRuleOverlap.RangesOverlap(D1, D2, D2, D3));
    }

    [Fact]
    public void RangesOverlap_PartialOverlap_Overlap()
    {
        Assert.True(TaskForwardingRuleOverlap.RangesOverlap(D1, D3, D2, D4));
    }

    [Fact]
    public void RangesOverlap_OpenEndedRanges_TreatedAsAlwaysActive()
    {
        // Both null (always active) always overlaps with anything.
        Assert.True(TaskForwardingRuleOverlap.RangesOverlap(null, null, D3, D4));
        // Open-ended "From" (started already) overlapping a future-only range still overlaps.
        Assert.True(TaskForwardingRuleOverlap.RangesOverlap(null, D2, D1, D3));
    }

    [Fact]
    public void RangesOverlap_OpenEndedNonOverlapping_DoNotOverlap()
    {
        Assert.False(TaskForwardingRuleOverlap.RangesOverlap(null, D1, D2, null));
    }
}
