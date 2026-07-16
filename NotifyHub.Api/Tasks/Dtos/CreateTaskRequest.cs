namespace NotifyHub.Api.Tasks.Dtos;

/// FR-008: priority defaults to "medium", due date system-suggested by priority — both
/// staff-overridable before saving, hence all fields here are optional.
public class CreateTaskRequest
{
    public string? Priority { get; set; }
    public DateTime? DueAt { get; set; }
    public bool IsRecurring { get; set; }
    public int? RecurrenceIntervalDays { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurrenceMaxOccurrences { get; set; }

    /// §1: pre-filled client-side from the thread's last message if omitted — see
    /// ThreadsController.CreateTask's server-side fallback, which does the same thing so a
    /// bare API call (no client) still gets a sensible default.
    public string? Description { get; set; }
    public string? TaskType { get; set; }

    /// Explicit assignee chosen by the creator; when omitted, ThreadsController.CreateTask
    /// falls back to the thread's current owner, then the configured default task
    /// provider, then the creator themselves.
    public long? AssignedStaffId { get; set; }
}
