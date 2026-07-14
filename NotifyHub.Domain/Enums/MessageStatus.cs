namespace NotifyHub.Domain.Enums;

public enum MessageStatus
{
    Queued,
    Sending,
    Sent,
    Delivered,
    Failed,

    /// BR-010: a still-queued reminder whose appointment was rescheduled (or removed)
    /// before it dispatched. Terminal — never picked up by the dispatcher.
    Superseded,

    /// P9-07: a still-Queued message whose ExpiresAt window passed before it dispatched.
    /// Terminal — never picked up by the dispatcher's Status == Queued query, same pattern
    /// as Superseded.
    Expired,

    /// P9-08 rule 28/29: a Reminder SMS the staff member cancelled before it dispatched.
    /// Terminal, same never-picked-up-again pattern as Expired/Superseded.
    Cancelled
}
