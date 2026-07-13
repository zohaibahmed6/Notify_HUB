using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Users;

namespace NotifyHub.Infrastructure.Escalation;

/// FR-008/BR-004: flags tasks past due_at not already escalated (or completed/cancelled),
/// reassigning them to a fallback Admin. "Overdue" = the instant now > due_at, no grace
/// period, evaluated fresh on each run. Does not touch original_owner_id (BR-007d) — a
/// completed recurring task's next occurrence still goes back to its original owner,
/// regardless of any escalation reassignment along the way.
public class EscalationJob(NotifyHubDbContext db, ILogger<EscalationJob> logger)
{
    private const int BatchSize = 100;

    public async Task<int> EscalateOverdueTasksAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var overdue = await db.Tasks
            .Where(t => t.DueAt < now
                && t.Status != NotifyHubTaskStatus.Escalated
                && t.Status != NotifyHubTaskStatus.Completed
                && t.Status != NotifyHubTaskStatus.Cancelled)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (overdue.Count == 0)
            return 0;

        var fallbackAdminId = await FallbackUserResolver.ResolveFallbackAdminIdAsync(db, ct);

        foreach (var task in overdue)
        {
            var previousAssignee = task.AssignedStaffId;
            task.Status = NotifyHubTaskStatus.Escalated;
            AuditLogger.Add(db, actor: "system", action: "escalation", entityType: "TaskItem", entityId: task.Id,
                detail: $"overdue since {task.DueAt:o}");

            if (fallbackAdminId is not null && task.AssignedStaffId != fallbackAdminId)
            {
                task.AssignedStaffId = fallbackAdminId;
                AuditLogger.Add(db, actor: "system", action: "assignment", entityType: "TaskItem", entityId: task.Id,
                    detail: $"auto-reassigned to Admin (was {previousAssignee?.ToString() ?? "unassigned"})");
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Escalation job: escalated {Count} overdue task(s)", overdue.Count);

        return overdue.Count;
    }
}
