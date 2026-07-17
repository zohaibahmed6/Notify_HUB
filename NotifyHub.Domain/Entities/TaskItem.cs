using NotifyHub.Domain.Enums;

namespace NotifyHub.Domain.Entities;

/// Named TaskItem (not Task) to avoid clashing with System.Threading.Tasks.Task. Maps to
/// the `tasks` table (§7). FR-008/BR-007/BR-014.
public class TaskItem
{
    public long Id { get; set; }

    public long ThreadId { get; set; }
    public ConversationThread Thread { get; set; } = default!;

    public TaskPriority Priority { get; set; }
    public DateTime DueAt { get; set; }
    public NotifyHubTaskStatus Status { get; set; }

    public long? AssignedStaffId { get; set; }
    public User? AssignedStaff { get; set; }

    /// Set whenever AssignedStaffId is actually changed (creation, PATCH, Forward, or a
    /// recurrence spawn) — null on legacy rows/never-assigned tasks, which sort last under a
    /// most-recently-assigned-first order (we genuinely don't know when they were assigned).
    public DateTime? AssignedAt { get; set; }

    /// Set at creation; recurrence always reassigns here regardless of who the previous
    /// occurrence was escalated to or completed by (BR-007d).
    public long OriginalOwnerId { get; set; }
    public User OriginalOwner { get; set; } = default!;

    public bool IsRecurring { get; set; }
    public int? RecurrenceIntervalDays { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurrenceMaxOccurrences { get; set; }
    public int OccurrenceCount { get; set; } = 1;

    public string? Description { get; set; }
    public TaskType TaskType { get; set; } = TaskType.General;

    /// List-filter flag only, independent of the workflow Status above — not a workflow
    /// eligibility concept (does not gate escalation/recurrence/forwarding).
    public bool IsActive { get; set; } = true;
}
