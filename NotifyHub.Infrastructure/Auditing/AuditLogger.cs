using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Auditing;

/// FR-011: every audited event (send, receipt, opt-out, assignment, escalation) goes
/// through here. Adds to the DbContext's change tracker without saving, so callers
/// persist the audit row atomically together with the rest of their own changes.
public static class AuditLogger
{
    public static void Add(
        NotifyHubDbContext db,
        string actor,
        string action,
        string entityType,
        long entityId,
        string? detail = null)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Actor = actor,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            OccurredAt = DateTime.UtcNow,
            Detail = detail,
        });
    }
}
