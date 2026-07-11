using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Common;
using NotifyHub.Api.Inbox;
using NotifyHub.Api.Tasks.Dtos;
using NotifyHub.Api.Threads.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Tasks;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// §8: Staff/Admin — no [Authorize(Roles=...)] needed, matches the default authenticated
/// policy exactly (same reasoning as TemplatesController).
[ApiController]
[Route("api/threads")]
public class ThreadsController(NotifyHubDbContext db, IHubContext<InboxHub> inboxHub) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ThreadDto>>> List(int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        (page, pageSize) = PagedResult<ThreadDto>.Clamp(page, pageSize);

        var query = db.Threads
            .Include(t => t.Patient)
            .Include(t => t.AssignedStaff)
            .OrderByDescending(t => t.Id);

        var totalCount = await query.CountAsync(ct);
        var threads = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        var items = threads.Select(ToDto).ToList();

        return Ok(new PagedResult<ThreadDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount });
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ThreadDetailDto>> Detail(long id, CancellationToken ct)
    {
        var thread = await db.Threads
            .Include(t => t.Patient)
            .Include(t => t.AssignedStaff)
            .Include(t => t.InboundMessages)
            .Include(t => t.OutboundMessages)
            .SingleOrDefaultAsync(t => t.Id == id, ct);

        if (thread is null)
            return NotFound();

        var messages = thread.InboundMessages
            .Select(m => new ThreadMessageDto
            {
                Direction = "inbound",
                Body = m.Body,
                Timestamp = m.ReceivedAt,
            })
            .Concat(thread.OutboundMessages.Select(m => new ThreadMessageDto
            {
                Direction = "outbound",
                SenderType = m.SenderType.ToString(),
                Body = m.RenderedBody ?? string.Empty,
                Timestamp = m.CreatedAt,
                Status = m.Status.ToString(),
            }))
            .OrderBy(m => m.Timestamp)
            .ToList();

        // §6c: unread count resets to 0 on opening the thread.
        thread.UnreadCount = 0;
        await db.SaveChangesAsync(ct);

        var dto = ToDto(thread);
        return Ok(new ThreadDetailDto
        {
            Id = dto.Id,
            PatientId = dto.PatientId,
            PatientName = dto.PatientName,
            PatientOptedOut = dto.PatientOptedOut,
            AssignedStaffId = dto.AssignedStaffId,
            AssignedStaffUsername = dto.AssignedStaffUsername,
            UnreadCount = dto.UnreadCount,
            Messages = messages,
        });
    }

    /// BR-001b: server-side opt-out enforcement applies to staff ad-hoc sends too, not
    /// just system dispatch — the frontend also disables the Send button, but that's UI
    /// only. The reply is queued like any other outbound message; the real dispatcher
    /// picks it up (re-checking opt-out again immediately before the gateway call, BR-001a).
    [HttpPost("{id}/messages")]
    public async Task<ActionResult> Reply(long id, ReplyRequest request, CancellationToken ct)
    {
        var thread = await db.Threads.Include(t => t.Patient).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (thread is null)
            return NotFound();

        if (thread.Patient.OptOutAt is not null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Patient has opted out; cannot send further messages.");
        }

        db.OutboundMessages.Add(new OutboundMessage
        {
            PatientId = thread.PatientId,
            ThreadId = thread.Id,
            TemplateId = null,
            SenderType = SenderType.Staff,
            TriggerReference = null,
            RenderedBody = request.Body,
            CreatedAt = DateTime.UtcNow,
            Status = MessageStatus.Queued,
            IdempotencyKey = null,
            AttemptCount = 0,
        });

        await db.SaveChangesAsync(ct);

        // FR-007: without this, a staff reply only appeared live in the sender's own tab
        // — other open sessions on the same thread wouldn't see it until their next
        // unrelated refetch. Mirrors the "threadAssigned" broadcast below.
        await inboxHub.Clients.All.SendAsync("outboundMessageSent", new { threadId = thread.Id }, ct);

        return NoContent();
    }

    /// BR-012: only succeeds if assigned_staff_id is currently null; concurrent assign
    /// attempt on an already-assigned thread returns 409.
    [HttpPost("{id}/assign")]
    public async Task<ActionResult> Assign(long id, AssignRequest request, CancellationToken ct)
    {
        var thread = await db.Threads.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (thread is null)
            return NotFound();

        if (thread.AssignedStaffId is not null)
            return Conflict();

        var callerId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var callerRole = User.FindFirstValue(ClaimTypes.Role);
        var targetStaffId = request.StaffId ?? callerId;

        if (targetStaffId != callerId && callerRole != nameof(UserRole.Admin))
            return Forbid();

        var targetUser = await db.Users.FindAsync([targetStaffId], ct);
        if (targetUser is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"User {targetStaffId} does not exist.");

        thread.AssignedStaffId = targetStaffId;
        var callerUsername = User.FindFirstValue(ClaimTypes.Name)!;
        AuditLogger.Add(db, actor: callerUsername, action: "assignment", entityType: "Thread", entityId: thread.Id,
            detail: $"assigned to {targetUser.Username}");

        await db.SaveChangesAsync(ct);
        await inboxHub.Clients.All.SendAsync("threadAssigned", new { threadId = thread.Id, assignedStaffId = targetStaffId }, ct);

        return NoContent();
    }

    [HttpPost("{id}/tasks")]
    public async Task<ActionResult<TaskDto>> CreateTask(long id, CreateTaskRequest request, CancellationToken ct)
    {
        var thread = await db.Threads.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (thread is null)
            return NotFound();

        var priority = TaskPriority.Medium;
        if (request.Priority is not null && !Enum.TryParse(request.Priority, ignoreCase: true, out priority))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Invalid priority '{request.Priority}'. Valid values: {string.Join(", ", Enum.GetNames<TaskPriority>())}.");
        }

        if (request.IsRecurring && request.RecurrenceIntervalDays is null)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "RecurrenceIntervalDays is required when IsRecurring is true.");
        }

        var now = DateTime.UtcNow;
        var dueAt = request.DueAt ?? TaskDueDateDefaults.DefaultDueAt(priority, now);

        // Default assignee: the thread's current owner if assigned, else the creator
        // (FR-008 default priority=medium on auto-creation doesn't specify assignee;
        // this mirrors "whoever is handling this thread owns the follow-up").
        var callerId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var assigneeId = thread.AssignedStaffId ?? callerId;

        var task = new TaskItem
        {
            ThreadId = thread.Id,
            Priority = priority,
            DueAt = dueAt,
            Status = NotifyHubTaskStatus.Open,
            AssignedStaffId = assigneeId,
            OriginalOwnerId = assigneeId,
            IsRecurring = request.IsRecurring,
            RecurrenceIntervalDays = request.RecurrenceIntervalDays,
            RecurrenceEndDate = request.RecurrenceEndDate,
            RecurrenceMaxOccurrences = request.RecurrenceMaxOccurrences,
            OccurrenceCount = 1,
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);

        var assignee = await db.Users.FindAsync([assigneeId], ct);
        return Created($"/api/tasks/{task.Id}", ToTaskDto(task, assignee?.Username));
    }

    private static ThreadDto ToDto(ConversationThread t) => new()
    {
        Id = t.Id,
        PatientId = t.PatientId,
        PatientName = t.Patient.Name,
        PatientOptedOut = t.Patient.OptOutAt is not null,
        AssignedStaffId = t.AssignedStaffId,
        AssignedStaffUsername = t.AssignedStaff?.Username,
        UnreadCount = t.UnreadCount,
    };

    private static TaskDto ToTaskDto(TaskItem t, string? assigneeUsername) => new()
    {
        Id = t.Id,
        ThreadId = t.ThreadId,
        Priority = t.Priority.ToString(),
        DueAt = t.DueAt,
        Status = t.Status.ToString(),
        AssignedStaffId = t.AssignedStaffId,
        AssignedStaffUsername = assigneeUsername,
        OriginalOwnerId = t.OriginalOwnerId,
        IsRecurring = t.IsRecurring,
        RecurrenceIntervalDays = t.RecurrenceIntervalDays,
        RecurrenceEndDate = t.RecurrenceEndDate,
        RecurrenceMaxOccurrences = t.RecurrenceMaxOccurrences,
        OccurrenceCount = t.OccurrenceCount,
    };
}
