using NotifyHub.Domain.Enums;

namespace NotifyHub.Domain.Entities;

public class DeliveryStatusHistory
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public OutboundMessage Message { get; set; } = default!;

    public MessageStatus Status { get; set; }
    public DateTime OccurredAt { get; set; }
}
