using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Settings;

namespace NotifyHub.Infrastructure.Messaging;

/// FR-001/FR-003: the core claim → render → dispatch step. A thin BackgroundService
/// (Worker's DispatcherWorker) just calls this in a loop; kept separate from the loop
/// itself so it's directly unit/integration-testable without hosting the Worker process.
public class MessageDispatcher(NotifyHubDbContext db, HttpClient gatewayClient, ILogger<MessageDispatcher> logger, SettingsService settingsService)
{
    private const int BatchSize = 10;

    public async Task<int> DispatchDueMessagesAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        // P9-07: checked unconditionally, before the Quiet Hours gate below — a message
        // sitting Queued through its whole 12h window while Quiet Hours suppresses the
        // batch entirely is the realistic way expiry gets hit in practice (BR-011's own
        // retry/backoff, max ~31 min across 6 attempts, almost never reaches 12h on its
        // own). If this ran after the Quiet Hours early-return, expiry would never fire
        // during the exact scenario that causes it.
        await ExpireOverdueMessagesAsync(now, ct);

        // §6: a single gate for the whole batch — during quiet hours, due messages just
        // stay Queued and are picked up on the next non-quiet poll. No per-message state
        // change, so nothing needs "un-queuing" once quiet hours end.
        if (await settingsService.IsQuietHoursNowAsync(ct))
            return 0;

        var due = await db.OutboundMessages
            .Include(m => m.Patient)
            .Include(m => m.Template)
            .Where(m => m.Status == MessageStatus.Queued
                && (m.NextRetryAt == null || m.NextRetryAt <= now)
                && (m.ScheduledAt == null || m.ScheduledAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in due)
            await DispatchOneAsync(message, ct);

        return due.Count;
    }

    /// P9-07: marks Expired any Queued message whose ExpiresAt window has passed, before
    /// any dispatch attempt is made for it — same terminal-status pattern as Superseded
    /// (BR-010), never picked up again by the Status == Queued due-query above.
    private async Task ExpireOverdueMessagesAsync(DateTime now, CancellationToken ct)
    {
        var overdue = await db.OutboundMessages
            .Where(m => m.Status == MessageStatus.Queued && m.ExpiresAt != null && m.ExpiresAt <= now)
            .ToListAsync(ct);

        if (overdue.Count == 0)
            return;

        foreach (var message in overdue)
        {
            message.Status = MessageStatus.Expired;
            // Fact-based, not a guess at the specific cause (e.g. "quiet hours") this
            // codebase has no per-message signal for — distinguishes only what's actually
            // knowable: whether a send was ever attempted before the window closed.
            message.ExpiryReason = message.AttemptCount == 0
                ? "Message expired before any send attempt was made."
                : $"Message expired after {message.AttemptCount} send attempt(s).";
            db.DeliveryStatusHistories.Add(new DeliveryStatusHistory
            {
                MessageId = message.Id,
                Status = MessageStatus.Expired,
                OccurredAt = now,
            });
            AuditLogger.Add(db, actor: "system", action: "expired", entityType: "OutboundMessage", entityId: message.Id,
                detail: message.ExpiryReason);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task DispatchOneAsync(OutboundMessage message, CancellationToken ct)
    {
        // BR-001a: checked immediately before calling the gateway, not only at
        // message-creation time, so a STOP arriving after a message is queued still
        // blocks it. Applies to both system-dispatched and staff ad-hoc messages.
        if (message.Patient.OptOutAt is not null)
        {
            message.Status = MessageStatus.Failed;
            message.NextRetryAt = null;
            AuditLogger.Add(db, actor: "system", action: "blocked", entityType: "OutboundMessage", entityId: message.Id,
                detail: "patient opted out, message not sent");
            await db.SaveChangesAsync(ct);
            return;
        }

        // Ad-hoc staff replies (TemplateId null) already have RenderedBody set directly
        // at creation — there's no template to render at send time (BR-008). Reminder SMS
        // (P9-08) used to always be TemplateId-linked with RenderedBody left null so it
        // rendered fresh here from the live template (rule 31); the Reminder dialog is now
        // freely editable and commits the caller's edited text as RenderedBody at creation,
        // so `RenderedBody is null` is what distinguishes "still needs a fresh render" from
        // "already has committed text, leave it alone" — a committed reminder body is only
        // overwritten again if something else (P9-05's template-edit sweep) explicitly nulls
        // RenderedBody back out first.
        if (message.TemplateId is not null && message.RenderedBody is null)
        {
            // Rendered here, at send time (not at creation), so history reflects the
            // template as it stood at the moment of send (BR-013).
            message.RenderedBody = await RenderAsync(message, ct);
        }

        message.Status = MessageStatus.Sending;
        await db.SaveChangesAsync(ct);

        try
        {
            var response = await gatewayClient.PostAsJsonAsync("api/mock-gateway/send", new { messageId = message.Id }, ct);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The gateway call itself failed (network/transport) rather than the
            // simulated carrier reporting a delivery failure — still counts as a
            // failed attempt against BR-011's 5-attempt cap.
            logger.LogWarning(ex, "Dispatcher: gateway call failed for message {MessageId}", message.Id);
            message.AttemptCount++;

            if (RetryBackoffPolicy.IsTerminal(message.AttemptCount))
            {
                message.Status = MessageStatus.Failed;
                message.NextRetryAt = null;
            }
            else
            {
                message.Status = MessageStatus.Queued;
                message.NextRetryAt = DateTime.UtcNow + RetryBackoffPolicy.NextDelay(message.AttemptCount);
            }

            await db.SaveChangesAsync(ct);
        }
    }

    private async Task<string> RenderAsync(OutboundMessage message, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["patient_name"] = message.Patient.Name,
        };

        // trigger_reference encodes the business event (BR-009), e.g. "appointment:{id}:created" —
        // parsed here to resolve {{appointment_time}} for appointment-reminder templates.
        if (message.TriggerReference is { } reference && reference.StartsWith("appointment:", StringComparison.Ordinal))
        {
            var parts = reference.Split(':');
            if (parts.Length >= 2 && long.TryParse(parts[1], out var appointmentId))
            {
                var appointment = await db.Appointments.FindAsync([appointmentId], ct);
                if (appointment is not null)
                    fields["appointment_time"] = appointment.ScheduledAt.ToString("u");
            }
        }

        return TemplateRenderer.Render(message.Template!.Body, fields);
    }
}
