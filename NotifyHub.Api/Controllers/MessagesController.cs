using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Common;
using NotifyHub.Api.Messages.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// P9-06's report (List) is Admin-only, matching AuditController's access pattern — but
/// P9-08's reminder edit/cancel actions are default-authenticated (Staff can manage
/// reminders they create from a thread, same as any other message action), so the
/// [Authorize(Roles="Admin")] restriction is per-action here, not class-level.
[ApiController]
[Route("api/messages")]
public class MessagesController(NotifyHubDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SmsHistoryPagedResult>> List(
        string? patientName, string? username, string? phone, string? text, string? status,
        DateTime? from, DateTime? to,
        int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        (page, pageSize) = PagedResult<SmsHistoryDto>.Clamp(page, pageSize);

        var query = db.OutboundMessages.AsQueryable();

        if (!string.IsNullOrWhiteSpace(patientName))
            query = query.Where(m => m.Patient.Name.Contains(patientName));

        // SentByUsername is null for system-dispatched sends, displayed/filtered as
        // "System" — same COALESCE-to-SQL translation used in the projection below.
        if (!string.IsNullOrWhiteSpace(username))
            query = query.Where(m => (m.SentByUsername ?? "System").Contains(username));

        if (!string.IsNullOrWhiteSpace(phone))
            query = query.Where(m => m.Patient.Phone.Contains(phone));

        if (!string.IsNullOrWhiteSpace(text))
            query = query.Where(m => m.RenderedBody != null && m.RenderedBody.Contains(text));

        if (status is not null)
        {
            if (!Enum.TryParse<MessageStatus>(status, ignoreCase: true, out var parsedStatus))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid status '{status}'. Valid values: {string.Join(", ", Enum.GetNames<MessageStatus>())}.");
            }

            query = query.Where(m => m.Status == parsedStatus);
        }

        if (from is not null)
            query = query.Where(m => m.CreatedAt >= from.Value);

        if (to is not null)
            query = query.Where(m => m.CreatedAt <= to.Value);

        query = query.OrderByDescending(m => m.CreatedAt);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new SmsHistoryDto
            {
                Id = m.Id,
                PatientName = m.Patient.Name,
                SenderUsername = m.SentByUsername ?? "System",
                Phone = m.Patient.Phone,
                Text = m.RenderedBody,
                Status = m.Status.ToString(),
                ScheduledTime = m.ScheduledAt,
                ExpiryTime = m.ExpiresAt, // wired for real now that P9-07 added ExpiresAt
                // PduCount: wired once P9-09 adds the column.
                PduCount = null,
            })
            .ToListAsync(ct);

        return Ok(new SmsHistoryPagedResult
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            // Sum of PduCount across the whole filtered set, not just this page — always 0
            // until P9-09 adds the column.
            TotalPduCount = 0,
        });
    }

    /// P9-08 rule 26/27: Event Time may be changed while a Reminder SMS is still Queued
    /// (recalculating Scheduled Send Time/Expiry Time from the message's own snapshotted
    /// offsets, rule 7 — not the current Settings values); once it's left Queued, "already
    /// been sent" per rule 27 blocks any further change.
    [HttpPatch("{id}")]
    public async Task<ActionResult> UpdateReminder(long id, UpdateReminderRequest request, CancellationToken ct)
    {
        var message = await db.OutboundMessages.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (message is null)
            return NotFound();

        if (message.EventTime is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Only Reminder SMS have an Event Time to update.");

        if (message.Status != MessageStatus.Queued)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Event Time can only be changed while the reminder is still Queued.");

        var offsetMinutes = message.ReminderOffsetMinutes!.Value;
        var expiryOffsetMinutes = message.ReminderExpiryOffsetMinutes!.Value;

        var newScheduledSendTime = ReminderScheduleCalculator.CalculateScheduledSendTime(request.EventTime, offsetMinutes);
        if (newScheduledSendTime <= DateTime.UtcNow)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                title: "The calculated Scheduled Send Time is already in the past — pick a later Event Time.");
        }

        var newIdempotencyKey = IdempotencyKeyGenerator.GenerateForReminder(message.PatientId, message.TemplateId!.Value, request.EventTime, offsetMinutes);
        if (newIdempotencyKey != message.IdempotencyKey && await db.OutboundMessages.AnyAsync(m => m.IdempotencyKey == newIdempotencyKey, ct))
        {
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "A reminder for this patient, template, and event time already exists.");
        }

        message.EventTime = request.EventTime;
        message.ScheduledAt = newScheduledSendTime;
        message.ExpiresAt = ReminderScheduleCalculator.CalculateExpiryTime(request.EventTime, expiryOffsetMinutes);
        message.IdempotencyKey = newIdempotencyKey;

        AuditLogger.Add(db, actor: User.FindFirstValue(ClaimTypes.Name)!, action: "reminder-updated", entityType: "OutboundMessage",
            entityId: message.Id, detail: $"event time changed to {request.EventTime:o}");
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// P9-08 rule 28/29: cancel is only meaningful for a Reminder SMS (EventTime set) that
    /// hasn't dispatched yet — same "still Queued" gate as the Event Time update above.
    [HttpPost("{id}/cancel")]
    public async Task<ActionResult> Cancel(long id, CancellationToken ct)
    {
        var message = await db.OutboundMessages.SingleOrDefaultAsync(m => m.Id == id, ct);
        if (message is null)
            return NotFound();

        if (message.EventTime is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Only Reminder SMS can be cancelled.");

        if (message.Status != MessageStatus.Queued)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Only a still-Queued reminder can be cancelled.");

        var now = DateTime.UtcNow;
        message.Status = MessageStatus.Cancelled;
        db.DeliveryStatusHistories.Add(new DeliveryStatusHistory { MessageId = message.Id, Status = MessageStatus.Cancelled, OccurredAt = now });
        AuditLogger.Add(db, actor: User.FindFirstValue(ClaimTypes.Name)!, action: "reminder-cancelled", entityType: "OutboundMessage", entityId: message.Id);

        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}
