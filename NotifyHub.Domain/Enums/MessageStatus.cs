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
    Superseded
}
