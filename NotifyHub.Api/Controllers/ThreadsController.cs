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
using NotifyHub.Domain.Messaging;
using NotifyHub.Domain.Tasks;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Settings;

namespace NotifyHub.Api.Controllers;

/// §8: Staff/Admin — no [Authorize(Roles=...)] needed, matches the default authenticated
/// policy exactly (same reasoning as TemplatesController).
[ApiController]
[Route("api/threads")]
public class ThreadsController(NotifyHubDbContext db, IHubContext<InboxHub> inboxHub, SettingsService settingsService) : ControllerBase
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
    public async Task<ActionResult<ThreadDetailDto>> Detail(long id, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        (page, pageSize) = PagedResult<ThreadMessageDto>.Clamp(page, pageSize);

        var thread = await db.Threads
            .Include(t => t.Patient)
            .Include(t => t.AssignedStaff)
            .SingleOrDefaultAsync(t => t.Id == id, ct);

        if (thread is null)
            return NotFound();

        var messages = await GetMessagesPageAsync(id, page, pageSize, ct);

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

    /// FR-010: merge-paginates the thread's inbound/outbound messages without loading its
    /// full history — the previous version's `.Include(InboundMessages).Include
    /// (OutboundMessages)` pulled every message for the thread on every open, which would
    /// become a real problem at high per-thread message volume. Page 1 = the most recent
    /// `pageSize` messages (opening a conversation shows its latest activity, chat-style);
    /// higher page numbers page backward into older history.
    ///
    /// Correctness of pulling only `skip+pageSize` rows per table (instead of everything):
    /// any message within the top `skip+pageSize` of the true merged-descending order must
    /// also be within the top `skip+pageSize` of its own table — it can't be preceded by
    /// that many messages from BOTH tables combined otherwise. So an ORDER BY DESC + LIMIT
    /// pushed to the database on each table separately is sufficient to correctly answer
    /// any page, without ever selecting the thread's entire history.
    private async Task<PagedResult<ThreadMessageDto>> GetMessagesPageAsync(long threadId, int page, int pageSize, CancellationToken ct)
    {
        var inboundTotal = await db.InboundMessages.CountAsync(m => m.ThreadId == threadId, ct);
        var outboundTotal = await db.OutboundMessages.CountAsync(m => m.ThreadId == threadId, ct);

        var skip = (page - 1) * pageSize;
        var take = skip + pageSize;

        var recentInbound = await db.InboundMessages
            .Where(m => m.ThreadId == threadId)
            .OrderByDescending(m => m.ReceivedAt)
            .Take(take)
            .Select(m => new ThreadMessageDto { Direction = "inbound", Body = m.Body, Timestamp = m.ReceivedAt })
            .ToListAsync(ct);

        var recentOutbound = await db.OutboundMessages
            .Where(m => m.ThreadId == threadId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .Select(m => new ThreadMessageDto
            {
                Direction = "outbound",
                SenderType = m.SenderType.ToString(),
                Body = m.RenderedBody ?? string.Empty,
                Timestamp = m.CreatedAt,
                Status = m.Status.ToString(),
            })
            .ToListAsync(ct);

        var items = recentInbound.Concat(recentOutbound)
            .OrderByDescending(m => m.Timestamp)
            .Skip(skip)
            .Take(pageSize)
            .OrderBy(m => m.Timestamp) // chat reading order within the page
            .ToList();

        return new PagedResult<ThreadMessageDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = inboundTotal + outboundTotal,
        };
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

        if (request.ScheduledAt is not null && request.ScheduledAt <= DateTime.UtcNow)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "ScheduledAt must be in the future.");
        }

        if (await RateLimitExceededAsync(thread.PatientId, ct))
        {
            return Problem(statusCode: StatusCodes.Status429TooManyRequests, title: "Per-patient outbound message rate limit exceeded.");
        }

        db.OutboundMessages.Add(new OutboundMessage
        {
            PatientId = thread.PatientId,
            ThreadId = thread.Id,
            TemplateId = null,
            SenderType = SenderType.Staff,
            SentByUsername = User.FindFirstValue(ClaimTypes.Name),
            TriggerReference = null,
            RenderedBody = request.Body,
            CreatedAt = DateTime.UtcNow,
            Status = MessageStatus.Queued,
            IdempotencyKey = null,
            AttemptCount = 0,
            ScheduledAt = request.ScheduledAt,
        });

        await db.SaveChangesAsync(ct);

        // FR-007: without this, a staff reply only appeared live in the sender's own tab
        // — other open sessions on the same thread wouldn't see it until their next
        // unrelated refetch. Mirrors the "threadAssigned" broadcast below.
        await inboxHub.Clients.All.SendAsync("outboundMessageSent", new { threadId = thread.Id }, ct);

        return NoContent();
    }

    /// §6: send SMS to a brand-new patient — creates the Patient + ConversationThread +
    /// first OutboundMessage in one call. Phone uniqueness is enforced the same way
    /// WebhooksController.FindOrCreateThreadAsync handles a concurrent insert (catch the
    /// unique-index violation), even though a staff-initiated single request is far less
    /// likely to race than concurrent inbound webhooks.
    [HttpPost]
    public async Task<ActionResult<ThreadDto>> CreateConversation(CreateConversationRequest request, CancellationToken ct)
    {
        if (request.ScheduledAt is not null && request.ScheduledAt <= DateTime.UtcNow)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "ScheduledAt must be in the future.");
        }

        if (await db.Patients.AnyAsync(p => p.Phone == request.Phone, ct))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: $"A patient with phone {request.Phone} already exists.");
        }

        var patient = new Patient { Name = request.Name, Phone = request.Phone };
        db.Patients.Add(patient);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: $"A patient with phone {request.Phone} already exists.");
        }

        var thread = new ConversationThread { PatientId = patient.Id, UnreadCount = 0 };
        db.Threads.Add(thread);
        await db.SaveChangesAsync(ct);

        db.OutboundMessages.Add(new OutboundMessage
        {
            PatientId = patient.Id,
            ThreadId = thread.Id,
            TemplateId = null,
            SenderType = SenderType.Staff,
            SentByUsername = User.FindFirstValue(ClaimTypes.Name),
            TriggerReference = null,
            RenderedBody = request.Message,
            CreatedAt = DateTime.UtcNow,
            Status = MessageStatus.Queued,
            IdempotencyKey = null,
            AttemptCount = 0,
            ScheduledAt = request.ScheduledAt,
        });
        await db.SaveChangesAsync(ct);

        return Created($"/api/threads/{thread.Id}", ToDto(new ConversationThread
        {
            Id = thread.Id,
            PatientId = patient.Id,
            Patient = patient,
            UnreadCount = 0,
        }));
    }

    /// §6: enforced at message-creation time (not inside the dispatcher) — counts the
    /// patient's OutboundMessages created within the configured window and feeds
    /// RateLimitChecker, a pure Domain calculator matching RetryBackoffPolicy's shape.
    private async Task<bool> RateLimitExceededAsync(long patientId, CancellationToken ct)
    {
        var rateLimit = await settingsService.GetRateLimitAsync(ct);
        if (!rateLimit.Enabled)
            return false;

        var windowStart = DateTime.UtcNow.AddHours(-rateLimit.WindowHours);
        var recentCount = await db.OutboundMessages.CountAsync(m => m.PatientId == patientId && m.CreatedAt >= windowStart, ct);

        return !RateLimitChecker.IsAllowed(recentCount, rateLimit.MaxMessages);
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

        // §7: Inactive/OnLeave users must not receive new work.
        if (targetUser.Status != UserStatus.Active)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: $"User {targetUser.Username} is not Active and cannot be assigned.");

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

        var taskType = TaskType.General;
        if (request.TaskType is not null && !Enum.TryParse(request.TaskType, ignoreCase: true, out taskType))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Invalid task type '{request.TaskType}'. Valid values: {string.Join(", ", Enum.GetNames<TaskType>())}.");
        }

        var now = DateTime.UtcNow;
        var dueAt = request.DueAt ?? TaskDueDateDefaults.DefaultDueAt(priority, now);

        // Default assignee: the thread's current owner if assigned, else the creator
        // (FR-008 default priority=medium on auto-creation doesn't specify assignee;
        // this mirrors "whoever is handling this thread owns the follow-up").
        var callerId = long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var assigneeId = thread.AssignedStaffId ?? callerId;

        // §1: auto-populate Description from the thread's most recent message (inbound or
        // outbound, whichever is newer) when the client didn't supply one — a server-side
        // fallback so a bare API call still gets a sensible default, matching how the
        // frontend pre-fills the same field before submit.
        var description = request.Description ?? await LatestMessageBodyAsync(thread.Id, ct);

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
            Description = description,
            TaskType = taskType,
        };

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);

        var assignee = await db.Users.FindAsync([assigneeId], ct);
        return Created($"/api/tasks/{task.Id}", ToTaskDto(task, assignee?.Username));
    }

    /// P9-04: resolves a template's merge fields to real values for the composer's insert-
    /// template preview — {{patient_name}} from the thread's actual patient,
    /// {{appointment_time}} from the patient's next real Scheduled appointment if one
    /// exists, else a generated future dummy time (so the preview always renders something
    /// plausible instead of leaving the token unresolved). The frontend fills this into the
    /// composer's editable textbox — not a locked preview. BR-013 is unaffected: rendered_body
    /// is still snapshotted at actual dispatch time (MessageDispatcher.RenderAsync) regardless
    /// of what was shown here, using whatever text staff ends up sending.
    [HttpGet("{id}/templates/{templateId}/preview")]
    public async Task<ActionResult<TemplatePreviewDto>> PreviewTemplate(long id, long templateId, CancellationToken ct)
    {
        var thread = await db.Threads.Include(t => t.Patient).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (thread is null)
            return NotFound();

        var template = await db.MessageTemplates.FindAsync([templateId], ct);
        if (template is null)
            return NotFound();

        var fields = new Dictionary<string, string>
        {
            ["patient_name"] = thread.Patient.Name,
        };

        var now = DateTime.UtcNow;
        var nextAppointment = await db.Appointments
            .Where(a => a.PatientId == thread.PatientId && a.Status == AppointmentStatus.Scheduled && a.ScheduledAt > now)
            .OrderBy(a => a.ScheduledAt)
            .FirstOrDefaultAsync(ct);

        var appointmentTime = nextAppointment?.ScheduledAt ?? now.Date.AddDays(3).AddHours(10);
        fields["appointment_time"] = appointmentTime.ToString("u");

        return Ok(new TemplatePreviewDto { RenderedBody = TemplateRenderer.Render(template.Body, fields) });
    }

    /// Compares the single most recent row from each table (both already indexed on their
    /// own timestamp — same "no full history load" reasoning as GetMessagesPageAsync above,
    /// just Take(1) instead of a page) rather than unioning and sorting the whole history.
    private async Task<string?> LatestMessageBodyAsync(long threadId, CancellationToken ct)
    {
        var latestInbound = await db.InboundMessages
            .Where(m => m.ThreadId == threadId)
            .OrderByDescending(m => m.ReceivedAt)
            .Select(m => new { m.Body, Timestamp = m.ReceivedAt })
            .FirstOrDefaultAsync(ct);

        var latestOutbound = await db.OutboundMessages
            .Where(m => m.ThreadId == threadId)
            .OrderByDescending(m => m.CreatedAt)
            .Select(m => new { Body = m.RenderedBody, Timestamp = m.CreatedAt })
            .FirstOrDefaultAsync(ct);

        if (latestInbound is null && latestOutbound is null)
            return null;

        if (latestInbound is null)
            return latestOutbound!.Body;

        if (latestOutbound is null)
            return latestInbound.Body;

        return latestInbound.Timestamp >= latestOutbound.Timestamp ? latestInbound.Body : latestOutbound.Body;
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
        Description = t.Description,
        TaskType = t.TaskType.ToString(),
        IsActive = t.IsActive,
    };
}
