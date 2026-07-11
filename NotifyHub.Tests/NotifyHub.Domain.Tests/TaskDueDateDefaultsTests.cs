using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Tasks;
using Xunit;

namespace NotifyHub.Domain.Tests;

public class TaskDueDateDefaultsTests
{
    private static readonly DateTime CreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(TaskPriority.Urgent, 4 * 60)]
    [InlineData(TaskPriority.High, 24 * 60)]
    [InlineData(TaskPriority.Medium, 3 * 24 * 60)]
    [InlineData(TaskPriority.Low, 7 * 24 * 60)]
    public void DefaultDueAt_MatchesPriorityOffset(TaskPriority priority, int expectedMinutesFromCreation)
    {
        var dueAt = TaskDueDateDefaults.DefaultDueAt(priority, CreatedAt);

        Assert.Equal(CreatedAt.AddMinutes(expectedMinutesFromCreation), dueAt);
    }
}
