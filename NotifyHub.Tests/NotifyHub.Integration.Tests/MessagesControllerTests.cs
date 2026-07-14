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
                    CreatedAt = DateTime.UtcNow,
                    AttemptCount = 0,
                },
                new OutboundMessage
                {
                    PatientId = patientId,
                    SenderType = SenderType.System,
                    SentByUsername = null,
                    RenderedBody = "P9-06 system-sent message",
                    Status = MessageStatus.Queued,
                    CreatedAt = DateTime.UtcNow,
                    AttemptCount = 0,
                });
            await db.SaveChangesAsync();
        }

        var (client, _) = await _client.AsAdminAsync();

        var all = await client.GetFromJsonAsync<SmsHistoryPagedResult>("/api/messages?phone=%2B19990000401");
        Assert.Equal(2, all!.TotalCount);
        Assert.Contains(all.Items, i => i.SenderUsername == CustomWebApplicationFactory.StaffUsername);
        Assert.Contains(all.Items, i => i.SenderUsername == "System");

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
