namespace NotifyHub.Domain.Messaging;

/// §6: Quiet Hours — a configurable UTC time-of-day window during which the dispatcher
/// pauses sending. Pure calculator (no DB/clock access) so the wrap-past-midnight logic
/// (e.g. 21:00-08:00) is unit-testable in isolation.
public static class QuietHoursCalculator
{
    public static bool IsQuietNow(TimeOnly nowUtc, TimeOnly start, TimeOnly end)
    {
        if (start == end)
            return false; // zero-width window - never quiet, avoids an accidental "always quiet" footgun

        return start < end
            ? nowUtc >= start && nowUtc < end
            : nowUtc >= start || nowUtc < end; // wraps past midnight
    }
}
