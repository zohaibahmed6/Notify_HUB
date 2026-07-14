using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.TaskForwarding.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Tasks;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// P9-10: self-service "forward my tasks to X" — every action here is scoped to the
/// caller's own UserId (rule 7's "a user cannot set themselves as their own forwarding
/// target" reads as first-person/self-service, and Settings → Task tab, where this is
/// configured, isn't Admin-gated like User Management is). Default authenticated, no
/// [Authorize(Roles=...)] needed.
[ApiController]
[Route("api/task-forwarding-rules")]
public class TaskForwardingRulesController(NotifyHubDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskForwardingRuleDto>>> List(CancellationToken ct)
    {
        var callerId = CallerId();

        var rules = await db.TaskForwardingRules
            .Include(r => r.TargetUser)
            .Where(r => r.UserId == callerId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => ToDto(r))
            .ToListAsync(ct);

        return Ok(rules);
    }

    [HttpPost]
    public async Task<ActionResult<TaskForwardingRuleDto>> Create(TaskForwardingRuleRequest request, CancellationToken ct)
    {
        var callerId = CallerId();

        // Rule 7: a user cannot set themselves as their own forwarding target.
        if (request.TargetUserId == callerId)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "You cannot set yourself as your own forwarding target.");

        // Rule 3: target can be any Active user.
        var target = await db.Users.FindAsync([request.TargetUserId], ct);
        if (target is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Target user not found.");
        if (target.Status != UserStatus.Active)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Forwarding target must be an Active user.");

        if (request.From is not null && request.To is not null && request.From > request.To)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "From must not be after To.");

        // Rules 4/9: no overlapping windows for the same user.
        if (await HasOverlapAsync(callerId, request.From, request.To, excludeRuleId: null, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This date range overlaps an existing forwarding rule for you.");

        var rule = new TaskForwardingRule
        {
            UserId = callerId,
            TargetUserId = request.TargetUserId,
            From = request.From,
            To = request.To,
            Reason = request.Reason,
            CreatedAt = DateTime.UtcNow,
        };
        db.TaskForwardingRules.Add(rule);
        await db.SaveChangesAsync(ct);

        rule.TargetUser = target;
        return Created($"/api/task-forwarding-rules/{rule.Id}", ToDto(rule));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<TaskForwardingRuleDto>> Update(long id, TaskForwardingRuleRequest request, CancellationToken ct)
    {
        var callerId = CallerId();
        var rule = await db.TaskForwardingRules.Include(r => r.TargetUser).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null || rule.UserId != callerId)
            return NotFound();

        if (request.TargetUserId == callerId)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "You cannot set yourself as your own forwarding target.");

        var target = await db.Users.FindAsync([request.TargetUserId], ct);
        if (target is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Target user not found.");
        if (target.Status != UserStatus.Active)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Forwarding target must be an Active user.");

        if (request.From is not null && request.To is not null && request.From > request.To)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "From must not be after To.");

        if (await HasOverlapAsync(callerId, request.From, request.To, excludeRuleId: id, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This date range overlaps an existing forwarding rule for you.");

        rule.TargetUserId = request.TargetUserId;
        rule.TargetUser = target;
        rule.From = request.From;
        rule.To = request.To;
        rule.Reason = request.Reason;
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(rule));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(long id, CancellationToken ct)
    {
        var callerId = CallerId();
        var rule = await db.TaskForwardingRules.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null || rule.UserId != callerId)
            return NotFound();

        // Rule 15: forwarding/audit *history* is permanently retained — that's the
        // per-task "forward" AuditLog entries already written at resolution time, which
        // this delete doesn't touch. The rule itself is just future-facing config.
        db.TaskForwardingRules.Remove(rule);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    private async Task<bool> HasOverlapAsync(long userId, DateTime? from, DateTime? to, long? excludeRuleId, CancellationToken ct)
    {
        var existing = await db.TaskForwardingRules
            .Where(r => r.UserId == userId && (excludeRuleId == null || r.Id != excludeRuleId))
            .Select(r => new { r.From, r.To })
            .ToListAsync(ct);

        return existing.Any(r => TaskForwardingRuleOverlap.RangesOverlap(from, to, r.From, r.To));
    }

    private long CallerId() => long.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static TaskForwardingRuleDto ToDto(TaskForwardingRule r) => new()
    {
        Id = r.Id,
        TargetUserId = r.TargetUserId,
        TargetUsername = r.TargetUser.Username,
        From = r.From,
        To = r.To,
        Reason = r.Reason,
        CreatedAt = r.CreatedAt,
    };
}
