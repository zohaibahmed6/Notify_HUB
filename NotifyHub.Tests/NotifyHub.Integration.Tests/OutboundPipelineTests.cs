using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// FR-013's required primary-workflow integration test: queue -> dispatch -> gateway ->
/// webhook -> status update. "Dispatch" is simulated by calling the mock-gateway endpoint
/// directly (what the real Worker dispatcher does over HTTP) rather than hosting the
/// separate Worker process, since WebApplicationFactory only boots Api.
file static class PipelineTestHelpers
{
    /// Simulates the dispatcher's claim step (render + Queued -> Sending) that would
    /// normally happen in MessageDispatcher (Infrastructure) before it calls the gateway.
    public static async Task<long> ClaimNextQueuedMessageAsync<TEntryPoint>(WebApplicationFactory<TEntryPoint> factory)
        where TEntryPoint : class
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var message = await db.OutboundMessages.FirstAsync(m => m.Status == MessageStatus.Queued);

        message.RenderedBody = "Rendered for integration test.";
        message.Status = MessageStatus.Sending;
        await db.SaveChangesAsync();

        return message.Id;
    }
}

public class OutboundPipelineHappyPathTests(ReliableGatewayWebApplicationFactory factory)
    : IClassFixture<ReliableGatewayWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Send_WhenGatewayDelivers_MovesMessageToDelivered()
    {
        var messageId = await PipelineTestHelpers.ClaimNextQueuedMessageAsync(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/mock-gateway/send")
        {
            Content = JsonContent.Create(new { messageId }),
        };
        request.Headers.Add("X-Webhook-Secret", CustomWebApplicationFactory.SharedSecret);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var assertScope = factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var updated = await assertDb.OutboundMessages.SingleAsync(m => m.Id == messageId);
        Assert.Equal(MessageStatus.Delivered, updated.Status);
        Assert.Equal(0, updated.AttemptCount);
        Assert.False(string.IsNullOrWhiteSpace(updated.RenderedBody));

        var history = await assertDb.DeliveryStatusHistories
            .Where(h => h.MessageId == messageId)
            .OrderBy(h => h.OccurredAt)
            .Select(h => h.Status)
            .ToListAsync();
        Assert.Equal([MessageStatus.Sent, MessageStatus.Delivered], history);

        var auditActions = await assertDb.AuditLogs
            .Where(a => a.EntityType == "OutboundMessage" && a.EntityId == messageId)
            .Select(a => a.Action)
            .ToListAsync();
        Assert.Equal(["send", "receipt"], auditActions);
    }
}

public class OutboundPipelineRetryTests(FailingGatewayWebApplicationFactory factory)
    : IClassFixture<FailingGatewayWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Send_WhenGatewayFails_SchedulesBackoffRetry()
    {
        var messageId = await PipelineTestHelpers.ClaimNextQueuedMessageAsync(factory);

        await SendAsync(messageId);

        using var assertScope = factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var updated = await assertDb.OutboundMessages.SingleAsync(m => m.Id == messageId);

        Assert.Equal(MessageStatus.Queued, updated.Status);
        Assert.Equal(1, updated.AttemptCount);
        Assert.NotNull(updated.NextRetryAt);
        Assert.True(updated.NextRetryAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Send_AfterFiveFailedAttempts_BecomesTerminalFailed()
    {
        var messageId = await PipelineTestHelpers.ClaimNextQueuedMessageAsync(factory);

        // Simulates the dispatcher re-attempting this message across 5 polling cycles —
        // each call re-fails deterministically (FailRatePercent=100). Only the first
        // attempt needs the claim/render step; the message stays Queued (not re-claimed
        // by a real dispatcher poll) between the remaining retries here.
        for (var i = 0; i < 5; i++)
            await SendAsync(messageId);

        using var assertScope = factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var updated = await assertDb.OutboundMessages.SingleAsync(m => m.Id == messageId);

        Assert.Equal(MessageStatus.Failed, updated.Status);
        Assert.Equal(5, updated.AttemptCount);
        Assert.Null(updated.NextRetryAt);
    }

    private async Task SendAsync(long messageId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/mock-gateway/send")
        {
            Content = JsonContent.Create(new { messageId }),
        };
        request.Headers.Add("X-Webhook-Secret", CustomWebApplicationFactory.SharedSecret);

        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }
}
