using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Common;
using NotifyHub.Api.Tasks.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Tasks;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// §8: Staff/Admin — no [Authorize(Roles=...)] needed, matches the default authenticated
/// policy exactly (same reasoning as TemplatesController).
[ApiController]
[Route("api/tasks")]
public class TasksController(NotifyHubDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskDto>>> List(
        string? status, long? assignedStaffId, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        (page, pageSize) = PagedResult<TaskDto>.Clamp(page, pageSize);

        var query = db.Tasks.Include(t => t.AssignedStaff).AsQueryable();

        if (status is not null)
        {
            if (!Enum.TryParse<NotifyHubTaskStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid status '{status}'. Valid values: {string.Join(", ", Enum.GetNames<NotifyHubTaskStatus>())}.");
            }

            query = query.Where(t => t.Status == parsedStatus);
        }

        if (assignedStaffId is not null)
            query = query.Where(t => t.AssignedStaffId == assignedStaffId);

        query = query.OrderBy(t => t.DueAt);

        var totalCount = await query.CountAsync(ct);
        var tasks = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = tasks.Select(ToDto).ToList();

        return Ok(new PagedResult<TaskDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TaskDto>> Detail(long id, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.AssignedStaff).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
            return NotFound();

        // BR-014: opening an escalated task is itself an "action taken" by the assignee.
        var callerId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        if (task.Status == NotifyHubTaskStatus.Escalated && task.AssignedStaffId == callerId)
        {
            task.Status = NotifyHubTaskStatus.InProgress;
            await db.SaveChangesAsync(ct);
        }

        return Ok(ToDto(task));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<TaskDto>> Update(long id, UpdateTaskRequest request, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.AssignedStaff).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
            return NotFound();

        var callerId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var isCurrentAssignee = task.AssignedStaffId == callerId;
        var wasEscalated = task.Status == NotifyHubTaskStatus.Escalated;

        if (request.Priority is not null)
        {
            if (!Enum.TryParse<TaskPriority>(request.Priority, ignoreCase: true, out var priority))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid priority '{request.Priority}'. Valid values: {string.Join(", ", Enum.GetNames<TaskPriority>())}.");
            }

            task.Priority = priority;
        }

        if (request.DueAt is not null)
            task.DueAt = request.DueAt.Value;

        if (request.AssignedStaffId is not null)
        {
            var targetUser = await db.Users.FindAsync([request.AssignedStaffId.Value], ct);
            if (targetUser is null)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"User {request.AssignedStaffId} does not exist.");

            task.AssignedStaffId = request.AssignedStaffId;
        }

        if (request.Status is not null)
        {
            if (!Enum.TryParse<NotifyHubTaskStatus>(request.Status, ignoreCase: true, out var newStatus))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid status '{request.Status}'. Valid values: {string.Join(", ", Enum.GetNames<NotifyHubTaskStatus>())}.");
            }

            // BR-007: only completing (not cancelling) spawns the next recurrence.
            if (newStatus == NotifyHubTaskStatus.Completed && task.IsRecurring)
                SpawnNextOccurrenceIfDue(task);

            task.Status = newStatus;
        }
        else if (wasEscalated && isCurrentAssignee)
        {
            // BR-014: any other action taken by the current assignee on an escalated
            // task auto-reverts it to in_progress, without a separate status change.
            task.Status = NotifyHubTaskStatus.InProgress;
        }

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(task));
    }

    /// BR-007: due-date-anchored (no drift), always reassigned to original_owner_id
    /// regardless of who completed or was escalated on the previous occurrence, stops
    /// when recurrence_end_date/recurrence_max_occurrences is reached.
    private void SpawnNextOccurrenceIfDue(TaskItem completed)
    {
        var next = RecurrenceCalculator.NextOccurrence(
            completed.DueAt,
            completed.RecurrenceIntervalDays!.Value,
            completed.OccurrenceCount,
            completed.RecurrenceEndDate,
            completed.RecurrenceMaxOccurrences);

        if (next is null)
            return;

        db.Tasks.Add(new TaskItem
        {
            ThreadId = completed.ThreadId,
            Priority = completed.Priority,
            DueAt = next.Value.DueAt,
            Status = NotifyHubTaskStatus.Open,
            AssignedStaffId = completed.OriginalOwnerId,
            OriginalOwnerId = completed.OriginalOwnerId,
            IsRecurring = true,
            RecurrenceIntervalDays = completed.RecurrenceIntervalDays,
            RecurrenceEndDate = completed.RecurrenceEndDate,
            RecurrenceMaxOccurrences = completed.RecurrenceMaxOccurrences,
            OccurrenceCount = next.Value.OccurrenceCount,
        });
    }

    private static TaskDto ToDto(TaskItem t) => new()
    {
        Id = t.Id,
        ThreadId = t.ThreadId,
        Priority = t.Priority.ToString(),
        DueAt = t.DueAt,
        Status = t.Status.ToString(),
        AssignedStaffId = t.AssignedStaffId,
        AssignedStaffUsername = t.AssignedStaff?.Username,
        OriginalOwnerId = t.OriginalOwnerId,
        IsRecurring = t.IsRecurring,
        RecurrenceIntervalDays = t.RecurrenceIntervalDays,
        RecurrenceEndDate = t.RecurrenceEndDate,
        RecurrenceMaxOccurrences = t.RecurrenceMaxOccurrences,
        OccurrenceCount = t.OccurrenceCount,
    };
}
