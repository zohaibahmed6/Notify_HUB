namespace NotifyHub.Api.Settings.Dtos;

public class SettingsDto
{
    public bool QuietHoursEnabled { get; set; }
    public string QuietHoursStart { get; set; } = default!;
    public string QuietHoursEnd { get; set; } = default!;
    public bool RateLimitEnabled { get; set; }
    public int RateLimitMaxMessages { get; set; }
    public int RateLimitWindowHours { get; set; }

    /// P9-08 rule 6/16 — current defaults applied to newly created Reminder SMS.
    public int ReminderOffsetMinutes { get; set; }
    public int ReminderExpiryOffsetMinutes { get; set; }
}
