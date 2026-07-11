namespace NotifyHub.Api.Audit.Dtos;

public class AuditLogDto
{
    public long Id { get; set; }
    public string Actor { get; set; } = default!;
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public long EntityId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Detail { get; set; }
}
