namespace NotifyHub.Api.Tasks.Dtos;

/// PATCH semantics: only non-null fields are applied. There's no supported way to
/// explicitly unassign a task (set AssignedStaffId back to null) via this endpoint —
/// not required by §8 and out of scope for this build.
public class UpdateTaskRequest
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public DateTime? DueAt { get; set; }
    public long? AssignedStaffId { get; set; }
    public string? Description { get; set; }
    public string? TaskType { get; set; }
    public bool? IsActive { get; set; }
}
