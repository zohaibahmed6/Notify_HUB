using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Audit.Dtos;
using NotifyHub.Api.Dashboard.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// Post-login landing page summary. Built last (increment 12) since it purely aggregates
/// Task/Thread/Audit data that's stable by now — no new business logic, just read-side
/// rollups. Default authenticated (every role gets a dashboard); org-wide task counts and
/// activity feed are Admin-only, mirroring AuditController's Admin/Staff split.
[ApiController]
[Route("api/dashboard")]
public class DashboardController(NotifyHubDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary(CancellationToken ct)
    {
        var callerId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerUsername = User.FindFirstValue(ClaimTypes.Name)!;
        var isAdmin = User.FindFirstValue(ClaimTypes.Role) == nameof(UserRole.Admin);

        var myTasks = await TaskCountsAsync(db.Tasks.Where(t => t.AssignedStaffId == callerId), ct);
        var orgTasks = isAdmin ? await TaskCountsAsync(db.Tasks, ct) : null;

        var unreadThreadCount = await db.Threads.CountAsync(t => t.UnreadCount > 0, ct);

        var recentActivityQuery = db.AuditLogs.AsQueryable();
        if (!isAdmin)
            recentActivityQuery = recentActivityQuery.Where(a => a.Actor == callerUsername);

        var recentActivity = await recentActivityQuery
            .OrderByDescending(a => a.OccurredAt)
            .Take(10)
            .ToListAsync(ct);

        return Ok(new DashboardSummaryDto
        {
            MyTasks = myTasks,
            OrgTasks = orgTasks,
            UnreadThreadCount = unreadThreadCount,
            RecentActivity = recentActivity.Select(ToAuditDto).ToList(),
        });
    }

    private static async Task<TaskCountsDto> TaskCountsAsync(IQueryable<TaskItem> query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        return new TaskCountsDto
        {
            Open = await query.CountAsync(t => t.Status == NotifyHubTaskStatus.Open, ct),
            InProgress = await query.CountAsync(t => t.Status == NotifyHubTaskStatus.InProgress, ct),
            Escalated = await query.CountAsync(t => t.Status == NotifyHubTaskStatus.Escalated, ct),
            Overdue = await query.CountAsync(t => t.DueAt < now
                && t.Status != NotifyHubTaskStatus.Completed
                && t.Status != NotifyHubTaskStatus.Cancelled, ct),
        };
    }

    private static AuditLogDto ToAuditDto(AuditLog a) => new()
    {
        Id = a.Id,
        Actor = a.Actor,
        Action = a.Action,
        EntityType = a.EntityType,
        EntityId = a.EntityId,
        OccurredAt = a.OccurredAt,
        Detail = a.Detail,
    };
}
