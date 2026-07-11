namespace NotifyHub.Domain.Entities;

/// A patient reply routed to their thread (FR-005).
public class InboundMessage
{
    public long Id { get; set; }

    public long ThreadId { get; set; }
    public ConversationThread Thread { get; set; } = default!;

    public string Body { get; set; } = default!;
    public DateTime ReceivedAt { get; set; }
}
