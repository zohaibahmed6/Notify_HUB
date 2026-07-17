using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.TaskForwarding.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Tasks;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// P9-10, opened up in a later session: "forward tasks from X to Y" — originally scoped to
/// the caller's own UserId (self-service only, rule 7 read as first-person), now any
/// authenticated user may configure a rule for any From/To pair (Settings → Task tab, where
/// this is configured, isn't Admin-gated like User Management is, and there's no per-rule
/// ownership check anymore — a deliberate scope-opening, not an oversight). Default
/// authenticated, no [Authorize(Roles=...)] needed.
[ApiController]
[Route("api/task-forwarding-rules")]
public class TaskForwardingRulesController(NotifyHubDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TaskForwardingRuleDto>>> List(CancellationToken ct)
    {
        var rules = await db.TaskForwardingRules
            .Include(r => r.User)
            .Include(r => r.TargetUser)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => ToDto(r))
            .ToListAsync(ct);

        return Ok(rules);
    }

    [HttpPost]
    public async Task<ActionResult<TaskForwardingRuleDto>> Create(TaskForwardingRuleRequest request, CancellationToken ct)
    {
        // A rule cannot forward a user's tasks to themselves.
        if (request.UserId == request.TargetUserId)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "From and To must be different users.");

        var fromUser = await db.Users.FindAsync([request.UserId], ct);
        if (fromUser is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "From user not found.");

        // Rule 3: target can be any Active user.
        var target = await db.Users.FindAsync([request.TargetUserId], ct);
        if (target is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Target user not found.");
        if (target.Status != UserStatus.Active)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Forwarding target must be an Active user.");

        if (request.From is not null && request.To is not null && request.From > request.To)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "From must not be after To.");

        // Rules 4/9: no overlapping windows for the same From user.
        if (await HasOverlapAsync(request.UserId, request.From, request.To, excludeRuleId: null, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This date range overlaps an existing forwarding rule for this user.");

        var rule = new TaskForwardingRule
        {
            UserId = request.UserId,
            TargetUserId = request.TargetUserId,
            From = request.From,
            To = request.To,
            Reason = request.Reason,
            CreatedAt = DateTime.UtcNow,
        };
        db.TaskForwardingRules.Add(rule);
        await db.SaveChangesAsync(ct);

        rule.User = fromUser;
        rule.TargetUser = target;
        return Created($"/api/task-forwarding-rules/{rule.Id}", ToDto(rule));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<TaskForwardingRuleDto>> Update(long id, TaskForwardingRuleRequest request, CancellationToken ct)
    {
        var rule = await db.TaskForwardingRules.Include(r => r.User).Include(r => r.TargetUser).SingleOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return NotFound();

        if (request.UserId == request.TargetUserId)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "From and To must be different users.");

        var fromUser = await db.Users.FindAsync([request.UserId], ct);
        if (fromUser is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "From user not found.");

        var target = await db.Users.FindAsync([request.TargetUserId], ct);
        if (target is null)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Target user not found.");
        if (target.Status != UserStatus.Active)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Forwarding target must be an Active user.");

        if (request.From is not null && request.To is not null && request.From > request.To)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "From must not be after To.");

        if (await HasOverlapAsync(request.UserId, request.From, request.To, excludeRuleId: id, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This date range overlaps an existing forwarding rule for this user.");

        rule.UserId = request.UserId;
        rule.User = fromUser;
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
        var rule = await db.TaskForwardingRules.SingleOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
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

    private static TaskForwardingRuleDto ToDto(TaskForwardingRule r) => new()
    {
        Id = r.Id,
        UserId = r.UserId,
        Username = r.User.Username,
        FullName = r.User.FullName,
        Role = r.User.Role.ToString(),
        TargetUserId = r.TargetUserId,
        TargetUsername = r.TargetUser.Username,
        TargetFullName = r.TargetUser.FullName,
        TargetRole = r.TargetUser.Role.ToString(),
        From = r.From,
        To = r.To,
        Reason = r.Reason,
        CreatedAt = r.CreatedAt,
    };
}
