using NotifyHub.Domain.Enums;

namespace NotifyHub.Domain.Tasks;

/// FR-008: system-suggested due date by priority, staff-overridable before saving.
public static class TaskDueDateDefaults
{
    public static DateTime DefaultDueAt(TaskPriority priority, DateTime createdAt) => priority switch
    {
        TaskPriority.Urgent => createdAt.AddHours(4),
        TaskPriority.High => createdAt.AddDays(1),
        TaskPriority.Medium => createdAt.AddDays(3),
        TaskPriority.Low => createdAt.AddDays(7),
        _ => throw new ArgumentOutOfRangeException(nameof(priority)),
    };
}
