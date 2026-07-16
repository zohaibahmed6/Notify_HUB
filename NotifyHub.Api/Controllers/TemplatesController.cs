using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Templates.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// §8: Admin or Staff — no [Authorize(Roles=...)] restriction needed since the global
/// policy (any authenticated user) already matches "Admin or Staff" exactly (§4: Staff
/// can manage templates, not Admin-only).
[ApiController]
[Route("api/templates")]
public class TemplatesController(NotifyHubDbContext db) : ControllerBase
{
    /// §5: `isActive` filter — omit to see everything (unlike Tasks, there's no
    /// "defaults to Active" requirement for Templates, just a filter control on the screen).
    /// `communicationMode` is a second, independent optional filter — used by the two
    /// send-time template pickers (composer "Insert template", Reminder SMS dialog) to
    /// only show Sms templates; the Templates management screen omits it to see every mode.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> List(bool? isActive, string? communicationMode, CancellationToken ct)
    {
        var query = db.MessageTemplates.AsQueryable();

        if (isActive is not null)
            query = query.Where(t => t.IsActive == isActive.Value);

        if (communicationMode is not null)
        {
            if (!Enum.TryParse<CommunicationMode>(communicationMode, ignoreCase: true, out var mode))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid communication mode '{communicationMode}'. Valid values: {string.Join(", ", Enum.GetNames<CommunicationMode>())}.");
            }

            query = query.Where(t => t.CommunicationMode == mode);
        }

        var templates = await query
            .OrderBy(t => t.Id)
            .Select(t => new TemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Body = t.Body,
                OffsetHours = t.OffsetHours,
                IsActive = t.IsActive,
                CommunicationMode = t.CommunicationMode.ToString(),
                BookmarkIds = t.Bookmarks.Select(b => b.Id).ToList(),
            })
            .ToListAsync(ct);

        return Ok(templates);
    }

    [HttpPost]
    public async Task<ActionResult<TemplateDto>> Create(CreateTemplateRequest request, CancellationToken ct)
    {
        var communicationMode = CommunicationMode.Sms;
        if (request.CommunicationMode is not null)
        {
            if (!Enum.TryParse(request.CommunicationMode, ignoreCase: true, out communicationMode))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid communication mode '{request.CommunicationMode}'. Valid values: {string.Join(", ", Enum.GetNames<CommunicationMode>())}.");
            }
        }

        var template = new MessageTemplate
        {
            Name = request.Name,
            Body = request.Body,
            OffsetHours = request.OffsetHours,
            CommunicationMode = communicationMode,
        };

        if (request.BookmarkIds is { Count: > 0 })
        {
            template.Bookmarks = await db.Bookmarks.Where(b => request.BookmarkIds.Contains(b.Id)).ToListAsync(ct);
        }

        db.MessageTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return Created($"/api/templates/{template.Id}", ToDto(template));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<TemplateDto>> Update(long id, UpdateTemplateRequest request, CancellationToken ct)
    {
        var template = await db.MessageTemplates.Include(t => t.Bookmarks).SingleOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound();

        if (request.Name is not null)
            template.Name = request.Name;

        if (request.Body is not null)
            template.Body = request.Body;

        if (request.OffsetHours is not null)
            template.OffsetHours = request.OffsetHours.Value;

        if (request.IsActive is not null)
            template.IsActive = request.IsActive.Value;

        if (request.CommunicationMode is not null)
        {
            if (!Enum.TryParse<CommunicationMode>(request.CommunicationMode, ignoreCase: true, out var mode))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid communication mode '{request.CommunicationMode}'. Valid values: {string.Join(", ", Enum.GetNames<CommunicationMode>())}.");
            }

            template.CommunicationMode = mode;
        }

        if (request.BookmarkIds is not null)
        {
            template.Bookmarks = await db.Bookmarks.Where(b => request.BookmarkIds.Contains(b.Id)).ToListAsync(ct);
        }

        await db.SaveChangesAsync(ct);

        // P9-05: dual-safety net #1 — explicit sweep of already-queued messages when the
        // body changes, so no stale-content SMS can go out. Net #2 is
        // MessageDispatcher.DispatchOneAsync, which already unconditionally re-renders
        // from the live template on every dispatch attempt for any TemplateId-linked
        // message (verified: RenderedBody is left null at creation by every current
        // production creation path — see ReminderScheduler — and dispatch always
        // overwrites it fresh before sending, retries included). Nulling it here is
        // currently redundant with that net for the happy path, but is kept per the
        // explicit dual-safety request and is the only net that would matter if a future
        // creation path ever pre-rendered RenderedBody instead of leaving it null.
        if (request.Body is not null)
        {
            if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            {
                var queuedMessages = await db.OutboundMessages
                    .Where(m => m.Status == MessageStatus.Queued && m.TemplateId == id)
                    .ToListAsync(ct);
                foreach (var message in queuedMessages)
                    message.RenderedBody = null;
                await db.SaveChangesAsync(ct);
            }
            else
            {
                await db.OutboundMessages
                    .Where(m => m.Status == MessageStatus.Queued && m.TemplateId == id)
                    .ExecuteUpdateAsync(s => s.SetProperty(m => m.RenderedBody, (string?)null), ct);
            }
        }

        return Ok(ToDto(template));
    }

    private static TemplateDto ToDto(MessageTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Body = t.Body,
        OffsetHours = t.OffsetHours,
        IsActive = t.IsActive,
        CommunicationMode = t.CommunicationMode.ToString(),
        BookmarkIds = t.Bookmarks.Select(b => b.Id).ToList(),
    };
}
