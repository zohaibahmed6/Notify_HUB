using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// Exercises FindOrCreateThreadAsync's catch(DbUpdateException) race guard
/// (WebhooksController.cs, guarded by threads.patient_id's unique index) against a
/// real MySQL database, where concurrent inserts genuinely race on connection/lock
/// timing — EF Core InMemory (used by every other integration test) can't reproduce
/// that interleaving, so this is the only test that actually exercises the catch
/// branch. Requires a real MySQL to be reachable (CI's service container, or locally
/// `docker compose up -d mysql`) — excluded from the default local run via the
/// MySql trait; run explicitly with `dotnet test --filter "Category=MySql"`.
[Trait("Category", "MySql")]
public class InboundWebhookThreadRaceMySqlTests(MySqlWebApplicationFactory factory)
    : IClassFixture<MySqlWebApplicationFactory>, IAsyncLifetime
{
    private const int ConcurrentRequests = 30;

    private readonly HttpClient _client = factory.CreateClient();
    private readonly string _phone = $"+1555{Guid.NewGuid():N}"[..14];
    private long _patientId;

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = new Patient { Name = "Race Condition Test Patient", Phone = _phone };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();
        _patientId = patient.Id;
    }

    public async Task DisposeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var threadIds = await db.Threads.Where(t => t.PatientId == _patientId)
            .Select(t => t.Id).ToListAsync();
        await db.InboundMessages.Where(m => threadIds.Contains(m.ThreadId)).ExecuteDeleteAsync();
        await db.Threads.Where(t => t.PatientId == _patientId).ExecuteDeleteAsync();
        await db.Patients.Where(p => p.Id == _patientId).ExecuteDeleteAsync();
    }

    [Fact]
    public async Task ConcurrentInbound_ForSamePatient_CreatesExactlyOneThread()
    {
        var responses = await Task.WhenAll(Enumerable.Range(0, ConcurrentRequests)
            .Select(_ => PostInboundAsync(_phone, "concurrent race test message")));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        // The unique index on threads.patient_id plus FindOrCreateThreadAsync's
        // catch(DbUpdateException) retry is what keeps this at exactly 1 despite
        // ConcurrentRequests racing writers — the assertion this whole test exists for.
        Assert.Equal(1, await db.Threads.CountAsync(t => t.PatientId == _patientId));

        // Each InboundMessage insert is independent (no shared mutable state), so this
        // count is safe to assert exactly — unlike thread.UnreadCount, which each
        // request increments via its own untracked DbContext scope and is a separate,
        // expected lost-update race unrelated to what this test targets.
        var thread = await db.Threads.SingleAsync(t => t.PatientId == _patientId);
        Assert.Equal(ConcurrentRequests, await db.InboundMessages.CountAsync(m => m.ThreadId == thread.Id));
    }

    private async Task<HttpResponseMessage> PostInboundAsync(string phone, string body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/inbound")
        {
            Content = JsonContent.Create(new { phone, body }),
        };
        request.Headers.Add("X-Webhook-Secret", MySqlWebApplicationFactory.SharedSecret);

        return await _client.SendAsync(request);
    }
}
