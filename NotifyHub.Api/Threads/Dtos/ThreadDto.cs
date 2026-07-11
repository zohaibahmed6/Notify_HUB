namespace NotifyHub.Api.Threads.Dtos;

public class ThreadDto
{
    public long Id { get; set; }
    public long PatientId { get; set; }
    public string PatientName { get; set; } = default!;
    public bool PatientOptedOut { get; set; }
    public long? AssignedStaffId { get; set; }
    public string? AssignedStaffUsername { get; set; }
    public int UnreadCount { get; set; }
}
