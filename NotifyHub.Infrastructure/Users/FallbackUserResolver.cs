using Microsoft.EntityFrameworkCore;
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
}
