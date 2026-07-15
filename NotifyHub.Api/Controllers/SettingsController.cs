using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Settings.Dtos;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Settings;

namespace NotifyHub.Api.Controllers;

/// §6/§8: admin-editable runtime config (Quiet Hours, per-patient rate limiting), backing
/// the Settings > SMS tab. Read is open to any authenticated user (Staff also benefits from
/// seeing the active Quiet Hours window); writes are Admin-only.
[ApiController]
[Route("api/settings")]
public class SettingsController(SettingsService settingsService, NotifyHubDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SettingsDto>> Get(CancellationToken ct)
    {
        var quietHours = await settingsService.GetQuietHoursAsync(ct);
        var rateLimit = await settingsService.GetRateLimitAsync(ct);
        var reminder = await settingsService.GetReminderAsync(ct);

        return Ok(new SettingsDto
        {
            QuietHoursEnabled = quietHours.Enabled,
            QuietHoursStart = quietHours.Start.ToString("HH:mm"),
            QuietHoursEnd = quietHours.End.ToString("HH:mm"),
            RateLimitEnabled = rateLimit.Enabled,
            RateLimitMaxMessages = rateLimit.MaxMessages,
            RateLimitWindowHours = rateLimit.WindowHours,
            ReminderOffsetMinutes = reminder.OffsetMinutes,
            ReminderExpiryOffsetMinutes = reminder.ExpiryOffsetMinutes,
            DefaultReminderTemplateId = reminder.DefaultTemplateId,
        });
    }

    [HttpPatch]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SettingsDto>> Update(UpdateSettingsRequest request, CancellationToken ct)
    {
        if (request.QuietHoursStart is not null && !TimeOnly.TryParse(request.QuietHoursStart, out _))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "QuietHoursStart must be a valid HH:mm time.");

        if (request.QuietHoursEnd is not null && !TimeOnly.TryParse(request.QuietHoursEnd, out _))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "QuietHoursEnd must be a valid HH:mm time.");

        if (request.RateLimitMaxMessages is <= 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "RateLimitMaxMessages must be positive.");

        if (request.RateLimitWindowHours is <= 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "RateLimitWindowHours must be positive.");

        if (request.ReminderOffsetMinutes is <= 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "ReminderOffsetMinutes must be positive.");

        if (request.ReminderExpiryOffsetMinutes is <= 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "ReminderExpiryOffsetMinutes must be positive.");

        if (request.DefaultReminderTemplateId is { } templateId && templateId > 0 && !await db.MessageTemplates.AnyAsync(t => t.Id == templateId, ct))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "DefaultReminderTemplateId does not reference an existing template.");

        var updates = new Dictionary<string, string>();

        if (request.QuietHoursEnabled is not null)
            updates[SettingsService.QuietHoursEnabledKey] = request.QuietHoursEnabled.Value.ToString();
        if (request.QuietHoursStart is not null)
            updates[SettingsService.QuietHoursStartKey] = request.QuietHoursStart;
        if (request.QuietHoursEnd is not null)
            updates[SettingsService.QuietHoursEndKey] = request.QuietHoursEnd;
        if (request.RateLimitEnabled is not null)
            updates[SettingsService.RateLimitEnabledKey] = request.RateLimitEnabled.Value.ToString();
        if (request.RateLimitMaxMessages is not null)
            updates[SettingsService.RateLimitMaxMessagesKey] = request.RateLimitMaxMessages.Value.ToString();
        if (request.RateLimitWindowHours is not null)
            updates[SettingsService.RateLimitWindowHoursKey] = request.RateLimitWindowHours.Value.ToString();
        if (request.ReminderOffsetMinutes is not null)
            updates[SettingsService.ReminderOffsetMinutesKey] = request.ReminderOffsetMinutes.Value.ToString();
        if (request.ReminderExpiryOffsetMinutes is not null)
            updates[SettingsService.ReminderExpiryOffsetMinutesKey] = request.ReminderExpiryOffsetMinutes.Value.ToString();
        if (request.DefaultReminderTemplateId is not null)
            updates[SettingsService.DefaultReminderTemplateIdKey] = request.DefaultReminderTemplateId.Value.ToString();

        if (updates.Count > 0)
            await settingsService.SetAsync(updates, ct);

        return await Get(ct);
    }

    /// Read-only diagnostics — not SystemSetting-backed, reflects actual config/DB state.
    [HttpGet("system-info")]
    public async Task<ActionResult<SystemInfoDto>> SystemInfo(CancellationToken ct)
    {
        return Ok(new SystemInfoDto
        {
            DatabaseConnected = await db.Database.CanConnectAsync(ct),
            DispatcherPollIntervalSeconds = 5, // NotifyHub.Worker/DispatcherWorker.cs, hardcoded (not config-driven)
            EscalationPollIntervalSeconds = configuration.GetValue("Escalation:PollIntervalSeconds", 60),
        });
    }
}
