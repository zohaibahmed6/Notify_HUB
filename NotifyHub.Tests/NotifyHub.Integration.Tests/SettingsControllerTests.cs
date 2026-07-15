using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Api.Settings.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// §6/§8: admin-editable Quiet Hours / rate-limit config, backing Settings > SMS.
public class SettingsControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_AnyAuthenticatedUser_SeesDefaults()
    {
        var (client, _) = await _client.AsStaffAsync();

        var settings = await client.GetFromJsonAsync<SettingsDto>("/api/settings");

        Assert.False(settings!.QuietHoursEnabled);
        Assert.False(settings.RateLimitEnabled);
        Assert.True(settings.RateLimitMaxMessages > 0);
    }

    [Fact]
    public async Task Update_AsAdmin_PersistsChanges()
    {
        var (client, _) = await _client.AsAdminAsync();

        var response = await client.PatchAsJsonAsync("/api/settings", new
        {
            quietHoursEnabled = true,
            quietHoursStart = "22:00",
            quietHoursEnd = "07:00",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.True(updated!.QuietHoursEnabled);
        Assert.Equal("22:00", updated.QuietHoursStart);
        Assert.Equal("07:00", updated.QuietHoursEnd);

        // Reset for other tests in this class's shared factory instance.
        await client.PatchAsJsonAsync("/api/settings", new { quietHoursEnabled = false });
    }

    [Fact]
    public async Task Update_AsStaff_Forbidden()
    {
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PatchAsJsonAsync("/api/settings", new { quietHoursEnabled = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Update_RejectsInvalidTime()
    {
        var (client, _) = await _client.AsAdminAsync();

        var response = await client.PatchAsJsonAsync("/api/settings", new { quietHoursStart = "not-a-time" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SystemInfo_ReportsDatabaseConnected()
    {
        var (client, _) = await _client.AsStaffAsync();

        var info = await client.GetFromJsonAsync<SystemInfoDto>("/api/settings/system-info");

        Assert.True(info!.DatabaseConnected);
        Assert.Equal(5, info.DispatcherPollIntervalSeconds);
    }

    [Fact]
    public async Task Update_SetsDefaultReminderTemplateId_RoundTripsViaGet()
    {
        var (client, _) = await _client.AsAdminAsync();
        var templateId = await SeedTemplateAsync("Default reminder template test");

        var response = await client.PatchAsJsonAsync("/api/settings", new { defaultReminderTemplateId = templateId });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.Equal(templateId, updated!.DefaultReminderTemplateId);

        var fetched = await client.GetFromJsonAsync<SettingsDto>("/api/settings");
        Assert.Equal(templateId, fetched!.DefaultReminderTemplateId);

        // Reset for other tests in this class's shared factory instance.
        await client.PatchAsJsonAsync("/api/settings", new { defaultReminderTemplateId = 0 });
    }

    [Fact]
    public async Task Update_ClearingDefaultReminderTemplateId_ZeroResetsToNull()
    {
        var (client, _) = await _client.AsAdminAsync();
        var templateId = await SeedTemplateAsync("Default reminder template clear test");
        await client.PatchAsJsonAsync("/api/settings", new { defaultReminderTemplateId = templateId });

        var response = await client.PatchAsJsonAsync("/api/settings", new { defaultReminderTemplateId = 0 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<SettingsDto>();
        Assert.Null(updated!.DefaultReminderTemplateId);
    }

    [Fact]
    public async Task Update_RejectsUnknownDefaultReminderTemplateId()
    {
        var (client, _) = await _client.AsAdminAsync();

        var response = await client.PatchAsJsonAsync("/api/settings", new { defaultReminderTemplateId = 999_999_999 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<long> SeedTemplateAsync(string name)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var template = new MessageTemplate
        {
            Name = name,
            Body = "Hi {{patient_name}}, this is a reminder.",
            TriggerType = TriggerType.AppointmentReminder,
            OffsetHours = 24,
        };
        db.MessageTemplates.Add(template);
        await db.SaveChangesAsync();

        return template.Id;
    }
}
