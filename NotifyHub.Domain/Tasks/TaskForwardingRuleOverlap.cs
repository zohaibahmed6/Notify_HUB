namespace NotifyHub.Domain.Tasks;

/// P9-10 rules 4/9: no two forwarding rules for the same user may have overlapping
/// (From, To) windows. Both bounds are open-ended when null — From null means "always
/// already started", To null means "never ends".
public static class TaskForwardingRuleOverlap
{
    public static bool RangesOverlap(DateTime? aFrom, DateTime? aTo, DateTime? bFrom, DateTime? bTo)
    {
        var aStart = aFrom ?? DateTime.MinValue;
        var aEnd = aTo ?? DateTime.MaxValue;
        var bStart = bFrom ?? DateTime.MinValue;
        var bEnd = bTo ?? DateTime.MaxValue;

        return aStart <= bEnd && bStart <= aEnd;
    }
}
