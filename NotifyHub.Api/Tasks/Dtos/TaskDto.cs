namespace NotifyHub.Api.Tasks.Dtos;

public class TaskDto
{
    public long Id { get; set; }
    public long ThreadId { get; set; }
    public string PatientName { get; set; } = default!;
    public string Priority { get; set; } = default!;
    public DateTime DueAt { get; set; }
    public string Status { get; set; } = default!;
    public long? AssignedStaffId { get; set; }
    public string? AssignedStaffUsername { get; set; }
    public long OriginalOwnerId { get; set; }
    public bool IsRecurring { get; set; }
    public int? RecurrenceIntervalDays { get; set; }
    public DateTime? RecurrenceEndDate { get; set; }
    public int? RecurrenceMaxOccurrences { get; set; }
    public int OccurrenceCount { get; set; }
    public string? Description { get; set; }
    public string TaskType { get; set; } = default!;
    public bool IsActive { get; set; }
}
