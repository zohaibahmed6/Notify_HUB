using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotifyHub.Api.Webhooks;
using NotifyHub.Api.Webhooks.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
[SharedSecret]
public class WebhooksController(NotifyHubDbContext db) : ControllerBase
{
    /// FR-002/FR-004: mock gateway's asynchronous delivery receipt. Moves the message
    /// to its terminal Delivered state, or back to Queued with backoff (or terminal
    /// Failed at BR-011's 5-attempt cap) on failure.
    [HttpPost("gateway-receipt")]
    public async Task<ActionResult> GatewayReceipt(GatewayReceiptRequest request, CancellationToken ct)
    {
        var message = await db.OutboundMessages.FindAsync([request.MessageId], ct);
        if (message is null)
            return NotFound();

        var now = DateTime.UtcNow;

        if (request.Delivered)
        {
            message.Status = MessageStatus.Delivered;
            message.NextRetryAt = null;
            db.DeliveryStatusHistories.Add(new DeliveryStatusHistory
            {
                MessageId = message.Id,
                Status = MessageStatus.Delivered,
                OccurredAt = now,
            });
            AuditLogger.Add(db, actor: "system", action: "receipt", entityType: "OutboundMessage", entityId: message.Id, detail: "delivered");
        }
        else
        {
            message.AttemptCount++;
            db.DeliveryStatusHistories.Add(new DeliveryStatusHistory
            {
                MessageId = message.Id,
                Status = MessageStatus.Failed,
                OccurredAt = now,
            });

            if (RetryBackoffPolicy.IsTerminal(message.AttemptCount))
            {
                message.Status = MessageStatus.Failed;
                message.NextRetryAt = null;
                AuditLogger.Add(db, actor: "system", action: "receipt", entityType: "OutboundMessage", entityId: message.Id,
                    detail: $"failed, terminal after {message.AttemptCount} attempts");
            }
            else
            {
                message.Status = MessageStatus.Queued;
                message.NextRetryAt = now + RetryBackoffPolicy.NextDelay(message.AttemptCount);
                AuditLogger.Add(db, actor: "system", action: "receipt", entityType: "OutboundMessage", entityId: message.Id,
                    detail: $"failed, retry {message.AttemptCount} scheduled for {message.NextRetryAt:o}");
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok();
    }
}
