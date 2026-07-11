namespace NotifyHub.Api.Threads.Dtos;

/// Null StaffId means "assign to me" (self-assign). A non-null StaffId assigning
/// someone else is Admin-only — enforced server-side (BR-005).
public class AssignRequest
{
    public long? StaffId { get; set; }
}
