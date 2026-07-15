namespace NotifyHub.Api.Settings.Dtos;

/// PATCH semantics: only non-null fields are applied.
public class UpdateSettingsRequest
{
    public bool? QuietHoursEnabled { get; set; }
    public string? QuietHoursStart { get; set; }
    public string? QuietHoursEnd { get; set; }
    public bool? RateLimitEnabled { get; set; }
    public int? RateLimitMaxMessages { get; set; }
    public int? RateLimitWindowHours { get; set; }
    public int? ReminderOffsetMinutes { get; set; }
    public int? ReminderExpiryOffsetMinutes { get; set; }

    /// 0 clears the default (real template ids are never 0).
    public long? DefaultReminderTemplateId { get; set; }
}
