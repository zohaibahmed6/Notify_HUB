namespace NotifyHub.Domain.Messaging;

/// BR-011: a message stops retrying after 5 failed attempts and moves to a terminal
/// Failed status. Backoff schedule: exponential — 1, 2, 4, 8, 16 minutes across the
/// 5 attempts allowed.
public static class RetryBackoffPolicy
{
    public const int MaxAttempts = 5;

    // Only the first 4 values are ever consumed — attempt 5 is terminal (IsTerminal),
    // so no 6th attempt is scheduled. The 5th value is kept to mirror FR-003's literal
    // published schedule (1/2/4/8/16 min) verbatim.
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
