using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Api.Messages.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// P9-06: GET /api/messages (SMS History report) — Admin-only, matches AuditController's
/// access pattern rather than the shared-inbox default-authenticated model.
public class MessagesControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task List_AsStaff_IsForbidden()
    {
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.GetAsync("/api/messages");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task List_AsAdmin_ReturnsSystemFallback_AndFilters()
    {
        long patientId;
        var staffCreatedAt = DateTime.UtcNow;
        var systemCreatedAt = DateTime.UtcNow;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var patient = new Patient { Name = "P9-06 Test Patient", Phone = "+19990000401" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();
            patientId = patient.Id;

            db.OutboundMessages.AddRange(
                new OutboundMessage
                {
                    PatientId = patientId,
                    SenderType = SenderType.Staff,
                    SentByUsername = CustomWebApplicationFactory.StaffUsername,
                    RenderedBody = "P9-06 staff-sent message",
                    Status = MessageStatus.Delivered,
                    CreatedAt = staffCreatedAt,
                    AttemptCount = 0,
                    PduCount = 3, // P9-09: receipt already landed
                },
                new OutboundMessage
                {
                    PatientId = patientId,
                    SenderType = SenderType.System,
                    SentByUsername = null,
                    RenderedBody = "P9-06 system-sent message",
                    Status = MessageStatus.Queued,
                    CreatedAt = systemCreatedAt,
                    AttemptCount = 0,
                    PduCount = null, // P9-09: no receipt yet, contributes 0 to the total
                });
            await db.SaveChangesAsync();
        }

        var (client, _) = await _client.AsAdminAsync();

        var all = await client.GetFromJsonAsync<SmsHistoryPagedResult>("/api/messages?phone=%2B19990000401");
        Assert.Equal(2, all!.TotalCount);
        Assert.Contains(all.Items, i => i.SenderUsername == CustomWebApplicationFactory.StaffUsername);
        Assert.Contains(all.Items, i => i.SenderUsername == "System");
        // P9-09: TotalPduCount sums across the whole filtered set (3 + null-as-0 = 3), and
        // the still-Queued row's PduCount is null (pending — no receipt yet).
        Assert.Equal(3, all.TotalPduCount);
        Assert.Equal(3, all.Items.Single(i => i.Status == "Delivered").PduCount);
        Assert.Null(all.Items.Single(i => i.Status == "Queued").PduCount);

        // Neither seeded row sets ScheduledAt (a direct/immediate send) — ScheduledTime
        // now falls back to CreatedAt instead of leaving the report's "Scheduled" column
        // blank for these rows.
        Assert.Equal(staffCreatedAt, all.Items.Single(i => i.Status == "Delivered").ScheduledTime, TimeSpan.FromSeconds(1));
        Assert.Equal(systemCreatedAt, all.Items.Single(i => i.Status == "Queued").ScheduledTime, TimeSpan.FromSeconds(1));

        var systemOnly = await client.GetFromJsonAsync<SmsHistoryPagedResult>("/api/messages?phone=%2B19990000401&username=System");
        Assert.Equal(1, systemOnly!.TotalCount);
        Assert.Equal("System", systemOnly.Items[0].SenderUsername);

        var byStatus = await client.GetFromJsonAsync<SmsHistoryPagedResult>("/api/messages?phone=%2B19990000401&status=Delivered");
        Assert.Equal(1, byStatus!.TotalCount);
        Assert.Equal(MessageStatus.Delivered.ToString(), byStatus.Items[0].Status);

        var byText = await client.GetFromJsonAsync<SmsHistoryPagedResult>("/api/messages?phone=%2B19990000401&text=staff-sent");
        Assert.Equal(1, byText!.TotalCount);
        Assert.Contains("staff-sent", byText.Items[0].Text);

        var byPatientName = await client.GetFromJsonAsync<SmsHistoryPagedResult>("/api/messages?patientName=P9-06%20Test%20Patient");
        Assert.Equal(2, byPatientName!.TotalCount);
    }
}
