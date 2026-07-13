namespace NotifyHub.Domain.Messaging;

/// §6: per-patient outbound message rate limiting. Pure calculator (no DB access) —
/// callers supply the count of messages already sent to a patient within the configured
/// window; matches the shape of RetryBackoffPolicy/IdempotencyKeyGenerator.
public static class RateLimitChecker
{
    public static bool IsAllowed(int recentMessageCount, int maxMessagesPerWindow) =>
        recentMessageCount < maxMessagesPerWindow;
}
