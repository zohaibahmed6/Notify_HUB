using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using NotifyHub.Api.Inbox;
using NotifyHub.Api.Webhooks;
using NotifyHub.Api.Webhooks.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Inbox;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
[SharedSecret]
public class WebhooksController(NotifyHubDbContext db, IHubContext<InboxHub> inboxHub) : ControllerBase
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

        // P9-09: set once, from whichever receipt lands first — immutable afterward (rule
        // 5, same audit-integrity principle as RenderedBody/BR-013), regardless of
        // Delivered/Failed outcome or how many further retry receipts follow.
        if (message.PduCount is null && request.PduCount is not null)
            message.PduCount = request.PduCount;

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

        // P9-02: the actual root cause of the "double tick" bug — no broadcast fired when
        // a receipt updated a message's delivery status, only at creation time
        // (outboundMessageSent). Same pattern as that broadcast/threadAssigned. Only
        // messages tied to a real thread have anything to invalidate client-side.
        if (message.ThreadId is not null)
        {
            await inboxHub.Clients.All.SendAsync("messageStatusUpdated", new
            {
                threadId = message.ThreadId,
                messageId = message.Id,
                status = message.Status.ToString(),
            }, ct);
        }

        return Ok();
    }

    /// FR-005/FR-006: simulated patient reply. Routes to the patient's thread
    /// (find-or-create, race-safe per threads.patient_id unique — BR-012's schema note),
    /// handles STOP-keyword opt-out (BR-001, BR-006), and pushes a real-time update to
    /// every connected inbox session (FR-007).
    [HttpPost("inbound")]
    public async Task<ActionResult> Inbound(InboundMessageRequest request, CancellationToken ct)
    {
        var patient = await db.Patients.SingleOrDefaultAsync(p => p.Phone == request.Phone, ct);
        if (patient is null)
            return NotFound();

        var thread = await FindOrCreateThreadAsync(patient.Id, ct);
        var now = DateTime.UtcNow;

        if (OptOutKeywordMatcher.IsOptOutRequest(request.Body) && patient.OptOutAt is null)
        {
            patient.OptOutAt = now;
            AuditLogger.Add(db, actor: "system", action: "opt-out", entityType: "Patient", entityId: patient.Id,
                detail: $"received keyword: {request.Body}");
        }

        db.InboundMessages.Add(new InboundMessage
        {
            ThreadId = thread.Id,
            Body = request.Body,
            ReceivedAt = now,
        });

        await db.SaveChangesAsync(ct);

        // Atomic UPDATE threads SET UnreadCount = UnreadCount + 1, not a read-modify-write
        // on the tracked entity — otherwise concurrent inbound webhooks for the same thread
        // can lose updates (both read the same starting value, both write the same +1 result).
        // ExecuteUpdateAsync's SetProperty can't be translated by the InMemory provider (used
        // by the fast test suite), which never exercises real concurrency anyway, so fall back
        // to the tracked increment there. Checked by provider name (not IsInMemory(), which
        // needs a package reference to Microsoft.EntityFrameworkCore.InMemory that this
        // project doesn't otherwise have) to avoid a test-only dependency in production code.
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            thread.UnreadCount++;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            await db.Threads.Where(t => t.Id == thread.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UnreadCount, t => t.UnreadCount + 1), ct);
        }

        await inboxHub.Clients.All.SendAsync("inboundMessageReceived", new
        {
            threadId = thread.Id,
            patientId = patient.Id,
            body = request.Body,
            receivedAt = now,
        }, ct);

        return Ok();
    }

    private async Task<ConversationThread> FindOrCreateThreadAsync(long patientId, CancellationToken ct)
    {
        var existing = await db.Threads.SingleOrDefaultAsync(t => t.PatientId == patientId, ct);
        if (existing is not null)
            return existing;

        var thread = new ConversationThread { PatientId = patientId, UnreadCount = 0 };
        db.Threads.Add(thread);

        try
        {
            await db.SaveChangesAsync(ct);
            return thread;
        }
        catch (DbUpdateException)
        {
            // A concurrent request won the race and already created this patient's
            // thread (threads.patient_id unique index) — detach our failed insert and
            // read back the one that won.
            db.Entry(thread).State = EntityState.Detached;
            return await db.Threads.SingleAsync(t => t.PatientId == patientId, ct);
        }
    }
}
