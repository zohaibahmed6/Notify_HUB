using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Settings;

public record QuietHoursSettings(bool Enabled, TimeOnly Start, TimeOnly End);
public record RateLimitSettings(bool Enabled, int MaxMessages, int WindowHours);
public record ReminderSettings(int OffsetMinutes, int ExpiryOffsetMinutes, long? DefaultTemplateId);

/// §6/§8: typed accessors over the generic SystemSetting key-value table — parsing/
/// validation/defaults live here once instead of scattered across controllers/consumers.
/// Defaults (both features start disabled — an admin opts in via Settings > SMS, so
/// existing dispatch behavior is unchanged out of the box) are seeded at startup by
/// SystemSettingSeedStep; these fallbacks only matter if a row is somehow missing.
public class SettingsService(NotifyHubDbContext db)
{
    public const string QuietHoursEnabledKey = "QuietHours:Enabled";
    public const string QuietHoursStartKey = "QuietHours:Start";
    public const string QuietHoursEndKey = "QuietHours:End";
    public const string RateLimitEnabledKey = "RateLimit:Enabled";
    public const string RateLimitMaxMessagesKey = "RateLimit:MaxMessages";
    public const string RateLimitWindowHoursKey = "RateLimit:WindowHours";

    /// P9-08: rule 6/16 — snapshotted onto each Reminder SMS at creation time (rule 7: a
    /// changed offset applies only to newly created reminders, never retroactively), not
    /// a gate like Quiet Hours/RateLimit — reminders are always computed using whatever
    /// values are current when created.
    public const string ReminderOffsetMinutesKey = "Reminder:OffsetMinutes";
    public const string ReminderExpiryOffsetMinutesKey = "Reminder:ExpiryOffsetMinutes";

    /// The template preselected when opening the Reminder SMS dialog from a thread — a
    /// convenience default only, never enforced server-side (staff can still pick any
    /// other active template). `0`/unparseable/absent all mean "no default" (real
    /// MessageTemplate ids are DB auto-increment, so 0 is never a real id).
    public const string DefaultReminderTemplateIdKey = "Reminder:DefaultTemplateId";

    public async Task<QuietHoursSettings> GetQuietHoursAsync(CancellationToken ct)
    {
        var values = await GetAllAsync(ct);

        return new QuietHoursSettings(
            Enabled: values.TryGetValue(QuietHoursEnabledKey, out var e) && bool.TryParse(e, out var enabled) && enabled,
            Start: values.TryGetValue(QuietHoursStartKey, out var s) && TimeOnly.TryParse(s, out var start) ? start : new TimeOnly(21, 0),
            End: values.TryGetValue(QuietHoursEndKey, out var end) && TimeOnly.TryParse(end, out var parsedEnd) ? parsedEnd : new TimeOnly(8, 0));
    }

    public async Task<RateLimitSettings> GetRateLimitAsync(CancellationToken ct)
    {
        var values = await GetAllAsync(ct);

        return new RateLimitSettings(
            Enabled: values.TryGetValue(RateLimitEnabledKey, out var e) && bool.TryParse(e, out var enabled) && enabled,
            MaxMessages: values.TryGetValue(RateLimitMaxMessagesKey, out var m) && int.TryParse(m, out var max) ? max : 20,
            WindowHours: values.TryGetValue(RateLimitWindowHoursKey, out var w) && int.TryParse(w, out var window) ? window : 24);
    }

    public async Task<ReminderSettings> GetReminderAsync(CancellationToken ct)
    {
        var values = await GetAllAsync(ct);

        return new ReminderSettings(
            OffsetMinutes: values.TryGetValue(ReminderOffsetMinutesKey, out var o) && int.TryParse(o, out var offset) ? offset : 1440,
            ExpiryOffsetMinutes: values.TryGetValue(ReminderExpiryOffsetMinutesKey, out var e) && int.TryParse(e, out var expiryOffset) ? expiryOffset : 15,
            DefaultTemplateId: values.TryGetValue(DefaultReminderTemplateIdKey, out var t) && long.TryParse(t, out var tid) && tid > 0 ? tid : null);
    }

    public async Task<bool> IsQuietHoursNowAsync(CancellationToken ct)
    {
        var quietHours = await GetQuietHoursAsync(ct);
        if (!quietHours.Enabled)
            return false;

        return QuietHoursCalculator.IsQuietNow(TimeOnly.FromDateTime(DateTime.UtcNow), quietHours.Start, quietHours.End);
    }

    public async Task<Dictionary<string, string>> GetAllAsync(CancellationToken ct) =>
        await db.SystemSettings.ToDictionaryAsync(s => s.Key, s => s.Value, ct);

    /// Upserts each provided key. Callers pass only the keys that changed.
    public async Task SetAsync(IReadOnlyDictionary<string, string> updates, CancellationToken ct)
    {
        var existing = await db.SystemSettings.Where(s => updates.Keys.Contains(s.Key)).ToDictionaryAsync(s => s.Key, ct);

        foreach (var (key, value) in updates)
        {
            if (existing.TryGetValue(key, out var row))
                row.Value = value;
            else
                db.SystemSettings.Add(new Domain.Entities.SystemSetting { Key = key, Value = value });
        }

        await db.SaveChangesAsync(ct);
    }
}
