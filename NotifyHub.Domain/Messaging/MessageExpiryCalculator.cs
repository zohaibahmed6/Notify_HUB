namespace NotifyHub.Domain.Messaging;

/// P9-07: Standard SMS expiry (rules 12-14 of P9-08's business-rule list, which covers
/// both SMS types — this calculator is the Standard SMS half; Reminder SMS gets its own
/// EventTime-anchored calculation in P9-08). Default 12h, anchored to CreatedAt for
/// immediate sends or ScheduledAt for scheduled sends — never both, never neither.
public static class MessageExpiryCalculator
{
    public static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(12);

    public static DateTime CalculateExpiresAt(DateTime createdAt, DateTime? scheduledAt) =>
        (scheduledAt ?? createdAt) + DefaultExpiry;
}
