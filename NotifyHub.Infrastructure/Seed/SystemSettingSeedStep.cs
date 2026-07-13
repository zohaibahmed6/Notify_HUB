using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Settings;

namespace NotifyHub.Infrastructure.Seed;

/// §6/§8: seeds default rows for every known setting key so GET /api/settings never
/// returns a gap. Idempotent per-key (not "any setting exists") so a future new setting
/// key can be added later without being skipped by an already-seeded install. Both
/// features start disabled — an admin opts in via Settings > SMS.
public class SystemSettingSeedStep : IDbSeedStep
{
    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        var existingKeys = await db.SystemSettings.Select(s => s.Key).ToListAsync(ct);

        var defaults = new Dictionary<string, string>
        {
            [SettingsService.QuietHoursEnabledKey] = "false",
            [SettingsService.QuietHoursStartKey] = "21:00",
            [SettingsService.QuietHoursEndKey] = "08:00",
            [SettingsService.RateLimitEnabledKey] = "false",
            [SettingsService.RateLimitMaxMessagesKey] = "20",
            [SettingsService.RateLimitWindowHoursKey] = "24",
        };

        var missing = defaults.Where(kv => !existingKeys.Contains(kv.Key));
        db.SystemSettings.AddRange(missing.Select(kv => new SystemSetting { Key = kv.Key, Value = kv.Value }));

        await db.SaveChangesAsync(ct);
    }
}
