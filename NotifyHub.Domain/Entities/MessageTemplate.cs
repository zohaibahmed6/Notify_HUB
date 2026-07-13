using NotifyHub.Domain.Enums;

namespace NotifyHub.Domain.Entities;

public class MessageTemplate
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;

    /// Supports {{field}} merge syntax (e.g. {{patient_name}}, {{appointment_time}}) — §9: max 1000 chars.
    public string Body { get; set; } = default!;

    public TriggerType TriggerType { get; set; }

    /// Hours before/after the trigger event this template should fire — §9: positive integer.
    public int OffsetHours { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<OutboundMessage> OutboundMessages { get; set; } = new List<OutboundMessage>();
}
