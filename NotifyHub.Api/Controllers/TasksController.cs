using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Common;
using NotifyHub.Api.Inbox;
using NotifyHub.Api.Tasks.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Tasks;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// §8: Staff/Admin — no [Authorize(Roles=...)] needed, matches the default authenticated
/// policy exactly (same reasoning as TemplatesController).
[ApiController]
[Route("api/tasks")]
public class TasksController(NotifyHubDbContext db, IHubContext<InboxHub> inboxHub) : ControllerBase
{
    /// §1: `description`/`patientName` are substring matches; `isActive` defaults to `true`
    /// when the client doesn't pass it at all (the task screen's own default is "Active"
    /// selected) — pass `isActive=false` explicitly to see inactive tasks, or omit the
    /// distinction entirely isn't supported (matches the "just a checkbox" filter model).
    [HttpGet]
    public async Task<ActionResult<PagedResult<TaskDto>>> List(
        string? status, long? assignedStaffId, string? description, string? patientName,
        DateTime? dueFrom, DateTime? dueTo, bool? isActive,
        int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        (page, pageSize) = PagedResult<TaskDto>.Clamp(page, pageSize);

        // Thread/Patient aren't eager-loaded (Include) since ToDto never reads them — the
        // patientName filter below still works via EF Core's automatic SQL join through
        // navigation-property access in a Where predicate, no Include required for that.
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

        if (!string.IsNullOrWhiteSpace(description))
            query = query.Where(t => t.Description != null && t.Description.Contains(description));

        if (!string.IsNullOrWhiteSpace(patientName))
            query = query.Where(t => t.Thread.Patient.Name.Contains(patientName));

        if (dueFrom is not null)
            query = query.Where(t => t.DueAt >= dueFrom.Value);

        if (dueTo is not null)
            query = query.Where(t => t.DueAt <= dueTo.Value);

        query = query.Where(t => t.IsActive == (isActive ?? true));

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

        if (request.Description is not null)
            task.Description = request.Description;

        if (request.TaskType is not null)
        {
            if (!Enum.TryParse<TaskType>(request.TaskType, ignoreCase: true, out var taskType))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid task type '{request.TaskType}'. Valid values: {string.Join(", ", Enum.GetNames<TaskType>())}.");
            }

            task.TaskType = taskType;
        }

        if (request.IsActive is not null)
            task.IsActive = request.IsActive.Value;

        if (request.AssignedStaffId is not null)
        {
            var targetUser = await db.Users.FindAsync([request.AssignedStaffId.Value], ct);
            if (targetUser is null)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"User {request.AssignedStaffId} does not exist.");

            // §7: Inactive/OnLeave users must not receive new work.
            if (targetUser.Status != UserStatus.Active)
                return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"User {targetUser.Username} is not Active and cannot be assigned.");

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

    /// §1: explicit manual forwarding, distinct from the plain PATCH AssignedStaffId path —
    /// always audited (PATCH's AssignedStaffId branch never has been, a pre-existing gap
    /// this doesn't retroactively fix) and rejects a non-Active target. Deliberately leaves
    /// the workflow Status untouched: BR-014's Escalated->InProgress auto-revert is about
    /// the *current assignee* taking action on their own task, not about who forwards it to
    /// them, so forwarding an Escalated task keeps it Escalated for the new assignee.
    [HttpPost("{id}/forward")]
    public async Task<ActionResult<TaskDto>> Forward(long id, ForwardTaskRequest request, CancellationToken ct)
    {
        var task = await db.Tasks.Include(t => t.AssignedStaff).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
            return NotFound();

        var targetUser = await db.Users.FindAsync([request.TargetUserId], ct);
        if (targetUser is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"User {request.TargetUserId} does not exist.");

        if (targetUser.Status != UserStatus.Active)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"User {targetUser.Username} is not Active and cannot be assigned.");

        var previousAssignee = task.AssignedStaff?.Username ?? "unassigned";
        task.AssignedStaffId = targetUser.Id;
        task.AssignedStaff = targetUser; // keep the loaded nav in sync so ToDto below isn't stale

        var callerUsername = User.FindFirstValue(ClaimTypes.Name)!;
        var detail = $"Task forwarded from {previousAssignee} to {targetUser.Username}" + (string.IsNullOrWhiteSpace(request.Note) ? "" : $": {request.Note}");
        AuditLogger.Add(db, actor: callerUsername, action: "forward", entityType: "TaskItem", entityId: task.Id, detail: detail);

        await db.SaveChangesAsync(ct);
        await inboxHub.Clients.All.SendAsync("taskAssignmentChanged", new { taskId = task.Id, assignedStaffId = targetUser.Id }, ct);

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
            TaskType = completed.TaskType, // category carries over; Description doesn't (it was tied to the message that prompted the completed occurrence, would go stale)
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
        Description = t.Description,
        TaskType = t.TaskType.ToString(),
        IsActive = t.IsActive,
    };
}
