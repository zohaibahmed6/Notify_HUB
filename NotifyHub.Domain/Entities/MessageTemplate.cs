using NotifyHub.Domain.Enums;

namespace NotifyHub.Domain.Entities;

public class MessageTemplate
{
    public long Id { get; set; }
    public string Name { get; set; } = default!;

    /// Supports {{field}} merge syntax (e.g. {{patient_name}}, {{appointment_time}}) — §9: max 1000 chars.
    public string Body { get; set; } = default!;

    /// Hours before/after the trigger event this template should fire — §9: positive integer.
    public int OffsetHours { get; set; }

    public bool IsActive { get; set; } = true;

    /// Which channel this template is intended for. Defaults to Sms so every pre-existing
    /// template keeps showing up in the SMS send-time pickers with no data fix needed.
    public CommunicationMode CommunicationMode { get; set; } = CommunicationMode.Sms;

    public ICollection<OutboundMessage> OutboundMessages { get; set; } = new List<OutboundMessage>();

    /// Bookmarks explicitly included in this template — a manifest for display purposes,
    /// not a hard dependency (removing a bookmark or unlinking it here doesn't touch the
    /// template's Body text).
    public ICollection<Bookmark> Bookmarks { get; set; } = new List<Bookmark>();
}
