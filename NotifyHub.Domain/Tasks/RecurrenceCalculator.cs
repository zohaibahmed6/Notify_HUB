namespace NotifyHub.Domain.Tasks;

/// BR-007: recurrence is due-date-anchored (no drift) and only spawned when the current
/// occurrence is completed — the caller is responsible for only invoking this on a
/// Completed transition (cancelling/any other status ends the series, per BR-007b).
public static class RecurrenceCalculator
{
    public readonly record struct Occurrence(DateTime DueAt, int OccurrenceCount);

    /// Returns null if the series has ended (BR-007c). Inference: "end date reached" is
    /// read as the next occurrence's due date landing on or after recurrenceEndDate;
    /// "exceeds max occurrences" is read literally (occurrence_count == max is still allowed).
    public static Occurrence? NextOccurrence(
        DateTime previousDueAt,
        int recurrenceIntervalDays,
        int previousOccurrenceCount,
        DateTime? recurrenceEndDate,
        int? recurrenceMaxOccurrences)
    {
        var nextDueAt = previousDueAt.AddDays(recurrenceIntervalDays);
        var nextOccurrenceCount = previousOccurrenceCount + 1;

        if (recurrenceEndDate is { } endDate && nextDueAt.Date >= endDate.Date)
            return null;

        if (recurrenceMaxOccurrences is { } max && nextOccurrenceCount > max)
            return null;

        return new Occurrence(nextDueAt, nextOccurrenceCount);
    }
}
