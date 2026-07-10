namespace NotifyHub.Domain.Entities;

/// FR-011: every send/receipt/opt-out/assignment/escalation event, with actor + timestamp.
public class AuditLog
{
    public long Id { get; set; }

    /// Username, or "system" for worker-originated events (e.g. dispatcher send, escalation).
    public string Actor { get; set; } = default!;

    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public long EntityId { get; set; }
    public DateTime OccurredAt { get; set; }

    /// Free-form detail (e.g. status transition, failure reason) — not structured/queried on.
    public string? Detail { get; set; }
}
