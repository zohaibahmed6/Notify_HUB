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
}
