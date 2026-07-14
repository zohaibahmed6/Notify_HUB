using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NotifyHub.Api.Gateway;
using NotifyHub.Api.Gateway.Dtos;
using NotifyHub.Api.Webhooks;
using NotifyHub.Api.Webhooks.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

/// FR-002: stands in for a real SMS carrier. Called by the Worker dispatcher
/// (service-to-service, no JWT — see SharedSecretAttribute) instead of an actual
/// carrier API. Not part of §8's public API surface.
[ApiController]
[Route("api/mock-gateway")]
[AllowAnonymous]
[SharedSecret]
public class MockGatewayController(
    NotifyHubDbContext db,
    IHttpClientFactory httpClientFactory,
    IOptions<MockGatewayOptions> options,
    ILogger<MockGatewayController> logger) : ControllerBase
{
    [HttpPost("send")]
    public async Task<ActionResult> Send(MockGatewaySendRequest request, CancellationToken ct)
    {
        var message = await db.OutboundMessages.FindAsync([request.MessageId], ct);
        if (message is null)
            return NotFound();

        var now = DateTime.UtcNow;
        message.Status = MessageStatus.Sent;
        message.SentAt = now; // P9-08 rule 32 "Sent Time" — applies to both SMS types (rule 22)
        db.DeliveryStatusHistories.Add(new DeliveryStatusHistory
        {
            MessageId = message.Id,
            Status = MessageStatus.Sent,
            OccurredAt = now,
        });
        AuditLogger.Add(db, actor: "system", action: "send", entityType: "OutboundMessage", entityId: message.Id);
        await db.SaveChangesAsync(ct);

        // Simulates carrier delay + random delivery outcome, then reports back via the
        // real webhook endpoint (shared-secret authenticated, same as an external caller
        // would be) — awaited here so the dispatcher's call, and any test driving it,
        // sees a fully settled outcome with no polling required.
        var opts = options.Value;
        var delayMs = opts.MaxDelayMs > opts.MinDelayMs
            ? Random.Shared.Next(opts.MinDelayMs, opts.MaxDelayMs + 1)
            : opts.MinDelayMs;
        await Task.Delay(delayMs, ct);
        var delivered = Random.Shared.Next(1, 101) > opts.FailRatePercent;

        var client = httpClientFactory.CreateClient("self");

        try
        {
            var response = await client.PostAsJsonAsync("api/webhooks/gateway-receipt",
                new GatewayReceiptRequest { MessageId = message.Id, Delivered = delivered }, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Mock gateway: failed to post delivery receipt for message {MessageId}", message.Id);
        }

        return Accepted();
    }
}
