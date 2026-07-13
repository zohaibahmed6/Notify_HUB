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
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TemplateDto>>> List(bool? isActive, CancellationToken ct)
    {
        var query = db.MessageTemplates.AsQueryable();

        if (isActive is not null)
            query = query.Where(t => t.IsActive == isActive.Value);

        var templates = await query
            .OrderBy(t => t.Id)
            .Select(t => new TemplateDto
            {
                Id = t.Id,
                Name = t.Name,
                Body = t.Body,
                TriggerType = t.TriggerType.ToString(),
                OffsetHours = t.OffsetHours,
                IsActive = t.IsActive,
            })
            .ToListAsync(ct);

        return Ok(templates);
    }

    [HttpPost]
    public async Task<ActionResult<TemplateDto>> Create(CreateTemplateRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<TriggerType>(request.TriggerType, ignoreCase: true, out var triggerType))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: $"Invalid trigger type '{request.TriggerType}'. Valid values: {string.Join(", ", Enum.GetNames<TriggerType>())}.");
        }

        var template = new MessageTemplate
        {
            Name = request.Name,
            Body = request.Body,
            TriggerType = triggerType,
            OffsetHours = request.OffsetHours,
        };

        db.MessageTemplates.Add(template);
        await db.SaveChangesAsync(ct);

        return Created($"/api/templates/{template.Id}", ToDto(template));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<TemplateDto>> Update(long id, UpdateTemplateRequest request, CancellationToken ct)
    {
        var template = await db.MessageTemplates.SingleOrDefaultAsync(t => t.Id == id, ct);
        if (template is null)
            return NotFound();

        if (request.Name is not null)
            template.Name = request.Name;

        if (request.Body is not null)
            template.Body = request.Body;

        if (request.TriggerType is not null)
        {
            if (!Enum.TryParse<TriggerType>(request.TriggerType, ignoreCase: true, out var triggerType))
            {
                return Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: $"Invalid trigger type '{request.TriggerType}'. Valid values: {string.Join(", ", Enum.GetNames<TriggerType>())}.");
            }

            template.TriggerType = triggerType;
        }

        if (request.OffsetHours is not null)
            template.OffsetHours = request.OffsetHours.Value;

        if (request.IsActive is not null)
            template.IsActive = request.IsActive.Value;

        await db.SaveChangesAsync(ct);
        return Ok(ToDto(template));
    }

    private static TemplateDto ToDto(MessageTemplate t) => new()
    {
        Id = t.Id,
        Name = t.Name,
        Body = t.Body,
        TriggerType = t.TriggerType.ToString(),
        OffsetHours = t.OffsetHours,
        IsActive = t.IsActive,
    };
}
