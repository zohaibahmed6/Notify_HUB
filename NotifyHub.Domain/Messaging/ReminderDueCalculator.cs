namespace NotifyHub.Domain.Messaging;

/// FR-009: a reminder for a given offset (48h/2h before appointment) is due once its
/// window opens and until the appointment itself occurs. A newly-created appointment
/// whose window already opened in the past (e.g. an appointment booked for tomorrow,
/// which is inside the 48h window immediately) is due right away, not skipped.
public static class ReminderDueCalculator
{
    public static bool IsDue(DateTime scheduledAt, int offsetHours, DateTime now)
        => now < scheduledAt && now >= scheduledAt.AddHours(-offsetHours);
}
