namespace NotifyHub.Domain.Messaging;

/// P9-08: pure Reminder SMS scheduling math (rules 5/9/15/17/18). Generic and independent
/// of the Appointment entity (rule 34) — EventTime is a raw caller-supplied instant, not
/// derived from any specific domain concept, so this is reusable for any future
/// event-based reminder (payments, document expiry, renewals, follow-ups).
public static class ReminderScheduleCalculator
{
    /// Rule 5: Scheduled Send Time = Event Time − Reminder Offset.
    public static DateTime CalculateScheduledSendTime(DateTime eventTime, int reminderOffsetMinutes) =>
        eventTime.AddMinutes(-reminderOffsetMinutes);

    /// Rule 15/17/18: Expiry Time = Event Time − Reminder Expiry Offset — never derived
    /// from Created Time or Scheduled Send Time, unlike Standard SMS (MessageExpiryCalculator).
    public static DateTime CalculateExpiryTime(DateTime eventTime, int reminderExpiryOffsetMinutes) =>
        eventTime.AddMinutes(-reminderExpiryOffsetMinutes);

    /// Rule 9: minimum selectable Event Time in the UI = Current Time + Reminder Offset —
    /// exactly the Event Time whose computed Scheduled Send Time equals "now".
    public static DateTime MinSelectableEventTime(DateTime now, int reminderOffsetMinutes) =>
        now.AddMinutes(reminderOffsetMinutes);
}
