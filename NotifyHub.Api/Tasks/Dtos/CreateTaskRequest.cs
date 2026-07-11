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
}
