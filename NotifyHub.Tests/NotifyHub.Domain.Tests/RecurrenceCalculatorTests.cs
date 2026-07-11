using NotifyHub.Domain.Tasks;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class RecurrenceCalculatorTests
{
    private static readonly DateTime PreviousDueAt = new(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NextOccurrence_IsAnchoredToPreviousDueDate_NoDrift()
    {
        var result = RecurrenceCalculator.NextOccurrence(
            previousDueAt: PreviousDueAt,
            recurrenceIntervalDays: 7,
            previousOccurrenceCount: 1,
            recurrenceEndDate: null,
            recurrenceMaxOccurrences: null);

        Assert.NotNull(result);
        Assert.Equal(PreviousDueAt.AddDays(7), result.Value.DueAt);
        Assert.Equal(2, result.Value.OccurrenceCount);
    }

    [Fact]
    public void NextOccurrence_UnboundedWhenNeitherLimitSet()
    {
        var result = RecurrenceCalculator.NextOccurrence(PreviousDueAt, 1, 100, null, null);

        Assert.NotNull(result);
    }

    [Fact]
    public void NextOccurrence_StopsWhenNextDueDateReachesEndDate()
    {
        // interval pushes the next due date to exactly the end date -> series ends.
        var endDate = PreviousDueAt.AddDays(7);

        var result = RecurrenceCalculator.NextOccurrence(PreviousDueAt, 7, 1, endDate, null);

        Assert.Null(result);
    }

    [Fact]
    public void NextOccurrence_ContinuesWhenNextDueDateIsBeforeEndDate()
    {
        var endDate = PreviousDueAt.AddDays(10);

        var result = RecurrenceCalculator.NextOccurrence(PreviousDueAt, 7, 1, endDate, null);

        Assert.NotNull(result);
    }

    [Fact]
    public void NextOccurrence_StopsWhenOccurrenceCountExceedsMax()
    {
        // previousOccurrenceCount=3, max=3 -> next would be 4, which exceeds 3.
        var result = RecurrenceCalculator.NextOccurrence(PreviousDueAt, 7, 3, null, 3);

        Assert.Null(result);
    }

    [Fact]
    public void NextOccurrence_AllowsOccurrenceCountEqualToMax()
    {
        // previousOccurrenceCount=2, max=3 -> next is 3, which does not exceed 3.
        var result = RecurrenceCalculator.NextOccurrence(PreviousDueAt, 7, 2, null, 3);

        Assert.NotNull(result);
        Assert.Equal(3, result.Value.OccurrenceCount);
    }
}
