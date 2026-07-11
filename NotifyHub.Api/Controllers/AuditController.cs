using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Audit.Dtos;
using NotifyHub.Api.Common;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// FR-011/§8: GET /api/audit is Admin-only (the first endpoint in this codebase that needs
/// the stricter [Authorize(Roles=...)] policy instead of the default any-authenticated one
/// — §4/§8 restrict "view full audit log, all actors" to Admin). GET /api/audit/mine is the
/// default authenticated policy, since §4/§8 grant it to Staff too, filtered server-side to
/// the caller's own actions only.
[ApiController]
[Route("api/audit")]
public class AuditController(NotifyHubDbContext db) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> List(
        string? actor, string? action, DateTime? from, DateTime? to, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        return Ok(await QueryAsync(actor, action, from, to, page, pageSize, ct));
    }

    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<AuditLogDto>>> Mine(
        string? action, DateTime? from, DateTime? to, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        var callerUsername = User.FindFirstValue(ClaimTypes.Name)!;
        return Ok(await QueryAsync(callerUsername, action, from, to, page, pageSize, ct));
    }

    private async Task<PagedResult<AuditLogDto>> QueryAsync(
        string? actor, string? action, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct)
    {
        (page, pageSize) = PagedResult<AuditLogDto>.Clamp(page, pageSize);

        var query = db.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(actor))
            query = query.Where(a => a.Actor == actor);

        if (!string.IsNullOrWhiteSpace(action))
            query = query.Where(a => a.Action == action);

        if (from is not null)
            query = query.Where(a => a.OccurredAt >= from.Value);

        if (to is not null)
            query = query.Where(a => a.OccurredAt <= to.Value);

        query = query.OrderByDescending(a => a.OccurredAt);

        var totalCount = await query.CountAsync(ct);
        var logs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = logs.Select(ToDto).ToList();

        return new PagedResult<AuditLogDto> { Items = items, Page = page, PageSize = pageSize, TotalCount = totalCount };
    }

    private static AuditLogDto ToDto(AuditLog a) => new()
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
