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
    Expired
}
