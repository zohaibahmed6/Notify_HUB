namespace NotifyHub.Domain.Messaging;

/// FR-009/BR-009/BR-010: builds and parses the trigger_reference used for appointment
/// reminders — "appointment:{appointmentId}:reminder:{offsetHours}h:{scheduledAtTicks}".
/// Embedding the appointment's ScheduledAt (rather than an incrementing version counter)
/// means a reschedule automatically produces a new reference with no extra schema field:
/// the old, still-queued reminder's embedded ticks simply stop matching the appointment's
/// current ScheduledAt, which is exactly the signal ReminderScheduler uses to supersede
/// it (BR-010). Still starts with "appointment:{id}:..." so MessageDispatcher.RenderAsync's
/// existing appointment-time parsing (which only reads parts[1]) keeps working unchanged.
public static class ReminderTriggerReference
{
    private const string EntityPrefix = "appointment";
    private const string Kind = "reminder";

    public static string Build(long appointmentId, int offsetHours, DateTime scheduledAt)
        => $"{EntityPrefix}:{appointmentId}:{Kind}:{offsetHours}h:{scheduledAt.Ticks}";

    /// False for anything that isn't exactly this reminder format — including the
    /// unrelated "appointment:{id}:created" references used by pre-scheduler seed data.
    public static bool TryParse(string reference, out long appointmentId, out int offsetHours, out long scheduledAtTicks)
    {
        appointmentId = 0;
        offsetHours = 0;
        scheduledAtTicks = 0;

        var parts = reference.Split(':');
        if (parts.Length != 5 || parts[0] != EntityPrefix || parts[2] != Kind)
            return false;

        if (!long.TryParse(parts[1], out appointmentId))
            return false;

        if (!parts[3].EndsWith('h') || !int.TryParse(parts[3][..^1], out offsetHours))
            return false;

        return long.TryParse(parts[4], out scheduledAtTicks);
    }
}
