using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Users;

/// Shared fallback-assignee lookup, used by both the overdue-task escalation job and
/// automatic task forwarding (a user transitioning to Inactive/OnLeave). Inference (same
/// as the pre-existing escalation behavior this was extracted from): BR-004 doesn't specify
/// which Admin when more than one exists — lowest-id *Active* Admin is the deterministic
/// fallback triage point. Excludes Inactive/OnLeave admins so a deactivated/on-leave admin
/// is never handed new work.
public static class FallbackUserResolver
{
    /// <param name="excludeUserId">
    /// Pass the id of a user currently being transitioned to Inactive/OnLeave in the same
    /// unit of work — their Status change isn't visible to this query yet (not yet saved),
    /// so without this they could still be selected as their own fallback.
    /// </param>
    public static Task<long?> ResolveFallbackAdminIdAsync(NotifyHubDbContext db, CancellationToken ct, long? excludeUserId = null)
    {
        var query = db.Users.Where(u => u.Role == UserRole.Admin && u.Status == UserStatus.Active);

        if (excludeUserId is not null)
            query = query.Where(u => u.Id != excludeUserId.Value);

        return query.OrderBy(u => u.Id).Select(u => (long?)u.Id).FirstOrDefaultAsync(ct);
    }

    /// P9-10: new-task-creation assignment only — deliberately a separate method from
    /// ResolveFallbackAdminIdAsync above rather than modifying it, since that method is
    /// also called by EscalationJob and UsersController's deactivation mass-reassignment,
    /// neither of which becomes forwarding-rule-aware (the deactivation mass-reassignment
    /// stays unchanged). Not a centralized "Assignment Engine" refactor — per the plan's
    /// own explicit scoping-down.
    ///
    /// Resolution order (changed in a later session — see docs/DECISIONS.md; originally
    /// STEP9_PLAN.md rule 1 gated this on the natural assignee being Inactive): look for a
    /// currently-in-window forwarding rule for <paramref name="naturalAssigneeId"/> first,
    /// unconditionally (rules 4/8/9 guarantee at most one can match "now"), regardless of
    /// whether the natural assignee is currently Active. If found and its target is itself
    /// Active, use the target (rule 3; one level only, rule 5 — the target's own rules, if
    /// any, are never followed). Otherwise (no rule matched, or the matched rule's target
    /// isn't Active — rule 6), use the natural assignee directly if they're Active. If
    /// neither applies, fall through to the existing plain Admin fallback.
    public static async Task<long> ResolveNewTaskAssigneeAsync(NotifyHubDbContext db, long naturalAssigneeId, CancellationToken ct)
    {
        var naturalAssignee = await db.Users.FindAsync([naturalAssigneeId], ct);

        var now = DateTime.UtcNow;
        var rule = await db.TaskForwardingRules
            .Where(r => r.UserId == naturalAssigneeId && (r.From == null || r.From <= now) && (r.To == null || r.To >= now))
            .FirstOrDefaultAsync(ct);

        if (rule is not null)
        {
            var target = await db.Users.FindAsync([rule.TargetUserId], ct);
            if (target is not null && target.Status == UserStatus.Active)
                return target.Id;
        }

        if (naturalAssignee is not null && naturalAssignee.Status == UserStatus.Active)
            return naturalAssigneeId;

        var fallbackAdminId = await ResolveFallbackAdminIdAsync(db, ct);
        return fallbackAdminId ?? naturalAssigneeId; // no Active Admin exists at all — degrade to the natural assignee rather than an invalid FK
    }
}
