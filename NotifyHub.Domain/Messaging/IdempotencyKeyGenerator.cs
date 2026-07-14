using System.Security.Cryptography;
using System.Text;

namespace NotifyHub.Domain.Messaging;

/// FR-003: idempotency key = deterministic hash of (patient_id + template_id + trigger_reference).
/// trigger_reference encodes the specific business event (BR-009), so a legitimate reschedule
/// produces a new key rather than being blocked as a duplicate of the original.
public static class IdempotencyKeyGenerator
{
    public static string Generate(long patientId, long templateId, string triggerReference)
    {
        var raw = $"{patientId}:{templateId}:{triggerReference}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// P9-08 rule 30: separate hash input from Standard SMS's Generate above (patientId +
    /// templateId + triggerReference) — prevents duplicate Reminder SMS for the same
    /// (recipient, template, event time, reminder offset) combination. eventTime.Ticks and
    /// reminderOffsetMinutes are both part of the key so a changed Event Time or a
    /// (rule-7) newly-created reminder under a different current offset both correctly
    /// produce a new key rather than colliding with an old one.
    public static string GenerateForReminder(long patientId, long templateId, DateTime eventTime, int reminderOffsetMinutes)
    {
        var raw = $"reminder:{patientId}:{templateId}:{eventTime.Ticks}:{reminderOffsetMinutes}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
