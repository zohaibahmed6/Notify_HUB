using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Common;
using NotifyHub.Api.Messages.Dtos;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// P9-06: SMS History report. Admin-only — matches AuditController's access pattern
/// (GET /api/audit), not the shared-inbox default-authenticated model every other
/// controller in this codebase uses.
[ApiController]
[Route("api/messages")]
[Authorize(Roles = "Admin")]
public class MessagesController(NotifyHubDbContext db) : ControllerBase
{
    [HttpGet]
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
                // ExpiryTime/PduCount: wired once P9-07/P9-09 add the underlying columns.
                ExpiryTime = null,
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
}
