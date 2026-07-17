using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Templates.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Messaging;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// §8: Admin or Staff — no [Authorize(Roles=...)] restriction needed since the global
/// policy (any authenticated user) already matches "Admin or Staff" exactly (§4: Staff
/// can manage templates, not Admin-only).
[ApiController]
[Route("api/templates")]
public class TemplatesController(NotifyHubDbContext db, MessageBodyRenderer bodyRenderer) : ControllerBase
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

        // P9-05: dual-safety net #1 — when the body changes, eagerly re-render every
        // already-Queued message linked to this template right now, using the fresh
        // template.Body, instead of nulling RenderedBody and leaving it for the dispatcher
        // to render lazily at actual send time. ScheduledAt/ExpiresAt/Status are untouched —
        // only content is refreshed, so send timing is unaffected. Net #2
        // (MessageDispatcher.DispatchOneAsync's `RenderedBody is null` guard) remains as a
        // defensive backstop for any row that still reaches dispatch without a rendered body
        // (e.g. a Reminder SMS created with no committed body, rule 31).
        if (request.Body is not null)
        {
            var queuedMessages = await db.OutboundMessages
                .Include(m => m.Patient)
                .Where(m => m.Status == MessageStatus.Queued && m.TemplateId == id)
                .ToListAsync(ct);

            foreach (var message in queuedMessages)
                message.RenderedBody = await bodyRenderer.RenderAsync(message, template.Body, ct);

            await db.SaveChangesAsync(ct);
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
