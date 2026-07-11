namespace NotifyHub.Domain.Messaging;

/// BR-011: a message stops retrying after 6 total attempts (1 initial send + 5 retries)
/// and moves to a terminal Failed status. Backoff schedule: exponential — 1, 2, 4, 8, 16
/// minutes, one delay between each of the 6 attempts (all 5 values used).
public static class RetryBackoffPolicy
{
    public const int MaxAttempts = 6;

    private static readonly TimeSpan[] Delays =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(4),
        TimeSpan.FromMinutes(8),
        TimeSpan.FromMinutes(16),
    ];

    public static bool IsTerminal(int attemptCount) => attemptCount >= MaxAttempts;

    /// attemptCount is the count of attempts made so far (1-based, i.e. after incrementing
    /// for the attempt that just failed). Throws if called for a terminal attempt count —
    /// callers must check IsTerminal first.
    public static TimeSpan NextDelay(int attemptCount)
    {
        if (IsTerminal(attemptCount))
            throw new InvalidOperationException($"No further retry after {attemptCount} attempts (max {MaxAttempts}).");

        return Delays[attemptCount - 1];
    }
}
