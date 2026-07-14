namespace NotifyHub.Domain.Entities;

/// P9-10: "forward my tasks to X" — checked before the existing always-fallback-to-Admin
/// logic (FallbackUserResolver) when a new task is created while its natural assignee is
/// Inactive/OnLeave. Does not replace that fallback; only supplements it.
public class TaskForwardingRule
{
    public long Id { get; set; }

    public long UserId { get; set; }
    public User User { get; set; } = default!;

    public long TargetUserId { get; set; }
    public User TargetUser { get; set; } = default!;

    /// Both nullable — an open-ended start/end. Rule 8: rules may be future-dated and
    /// activate/deactivate automatically based on these, without any background job (just
    /// compared against "now" at resolution time).
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// Rule 14: optional.
    public string? Reason { get; set; }

    public DateTime CreatedAt { get; set; }
}
