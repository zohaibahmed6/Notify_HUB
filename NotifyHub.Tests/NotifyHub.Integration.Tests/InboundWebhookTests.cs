using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// FR-005/FR-006: inbound webhook routes replies to the patient's thread and handles
/// STOP-keyword opt-out.
public class InboundWebhookTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    // Matches PatientAppointmentSeedStep's deterministic demo phone numbers (10 seeded).
    // Each test uses a distinct patient — all methods in this class share one DB via
    // IClassFixture, so reusing a phone number across tests would cross-contaminate
    // thread/unread-count state (same pitfall fixed in OutboundPipelineRetryTests).
    private const string CreatesThreadPhone = "+15550100001";
    private const string ReusesThreadPhone = "+15550100002";
    private const string StopKeywordPhone = "+15550100003";

    [Fact]
    public async Task Inbound_CreatesThread_WhenNoneExists()
    {
        var response = await PostInboundAsync(CreatesThreadPhone, "Hi, running a bit late.");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = await db.Patients.SingleAsync(p => p.Phone == CreatesThreadPhone);
        var thread = await db.Threads.SingleAsync(t => t.PatientId == patient.Id);
        var message = await db.InboundMessages.SingleAsync(m => m.ThreadId == thread.Id);

        Assert.Equal("Hi, running a bit late.", message.Body);
        Assert.Equal(1, thread.UnreadCount);
    }

    [Fact]
    public async Task Inbound_SecondMessage_ReusesExistingThread_AndIncrementsUnread()
    {
        await PostInboundAsync(ReusesThreadPhone, "first message");
        await PostInboundAsync(ReusesThreadPhone, "second message");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = await db.Patients.SingleAsync(p => p.Phone == ReusesThreadPhone);
        var threadCount = await db.Threads.CountAsync(t => t.PatientId == patient.Id);
        var thread = await db.Threads.SingleAsync(t => t.PatientId == patient.Id);

        Assert.Equal(1, threadCount);
        Assert.Equal(2, thread.UnreadCount);
    }

    [Fact]
    public async Task Inbound_WithStopKeyword_SetsOptOutAndAudits()
    {
        await PostInboundAsync(StopKeywordPhone, "STOP");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = await db.Patients.SingleAsync(p => p.Phone == StopKeywordPhone);
        Assert.NotNull(patient.OptOutAt);

        var audit = await db.AuditLogs.SingleAsync(a => a.EntityType == "Patient" && a.EntityId == patient.Id && a.Action == "opt-out");
        Assert.Equal("system", audit.Actor);
    }

    [Fact]
    public async Task Inbound_UnknownPhone_ReturnsNotFound()
    {
        var response = await PostInboundAsync("+19995550000", "hello");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostInboundAsync(string phone, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/inbound")
        {
            Content = JsonContent.Create(new { phone, body }),
        };
        request.Headers.Add("X-Webhook-Secret", CustomWebApplicationFactory.SharedSecret);

        return await _client.SendAsync(request);
    }
}
