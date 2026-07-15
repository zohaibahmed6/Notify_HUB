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

        var involvedUserIds = overdue.Select(t => t.AssignedStaffId).Where(id => id is not null).Select(id => id!.Value).ToHashSet();
        if (fallbackAdminId is not null)
            involvedUserIds.Add(fallbackAdminId.Value);
        var usernames = await db.Users.Where(u => involvedUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username, ct);

        foreach (var task in overdue)
        {
            var previousAssignee = task.AssignedStaffId;
            task.Status = NotifyHubTaskStatus.Escalated;
            AuditLogger.Add(db, actor: "system", action: "escalation", entityType: "TaskItem", entityId: task.Id,
                detail: $"overdue since {task.DueAt:o}");

            if (fallbackAdminId is not null && task.AssignedStaffId != fallbackAdminId)
            {
                task.AssignedStaffId = fallbackAdminId;
                var previousUsername = previousAssignee is { } prevId && usernames.TryGetValue(prevId, out var prevName) ? prevName : "unassigned";
                AuditLogger.Add(db, actor: "system", action: "assignment", entityType: "TaskItem", entityId: task.Id,
                    detail: $"Task auto-reassigned from {previousUsername} to {usernames[fallbackAdminId.Value]} (escalated, overdue)");
            }
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Escalation job: escalated {Count} overdue task(s)", overdue.Count);

        return overdue.Count;
    }

    /// P9-12: auto-reverts OnLeave -> Active once LeaveTo passes. Piggybacks on this
    /// existing periodic job/poll rather than a new worker process, per the plan's own
    /// instruction — unrelated to task escalation, just co-located for the free poll loop.
    public async Task<int> RevertExpiredLeaveAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var expired = await db.Users
            .Where(u => u.Status == UserStatus.OnLeave && u.LeaveTo != null && u.LeaveTo < now)
            .ToListAsync(ct);

        if (expired.Count == 0)
            return 0;

        foreach (var user in expired)
        {
            user.Status = UserStatus.Active;
            AuditLogger.Add(db, actor: "system", action: "status-change", entityType: "User", entityId: user.Id,
                detail: $"auto-reverted OnLeave -> Active (LeaveTo {user.LeaveTo:o} passed)");
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Escalation job: reverted {Count} On-Leave user(s) to Active", expired.Count);

        return expired.Count;
    }
}
