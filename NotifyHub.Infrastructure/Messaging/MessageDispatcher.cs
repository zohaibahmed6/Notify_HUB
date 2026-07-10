using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Messaging;

/// FR-001/FR-003: the core claim → render → dispatch step. A thin BackgroundService
/// (Worker's DispatcherWorker) just calls this in a loop; kept separate from the loop
/// itself so it's directly unit/integration-testable without hosting the Worker process.
public class MessageDispatcher(NotifyHubDbContext db, HttpClient gatewayClient, ILogger<MessageDispatcher> logger)
{
    private const int BatchSize = 10;

    public async Task<int> DispatchDueMessagesAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var due = await db.OutboundMessages
            .Include(m => m.Patient)
            .Include(m => m.Template)
            .Where(m => m.Status == MessageStatus.Queued && (m.NextRetryAt == null || m.NextRetryAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in due)
            await DispatchOneAsync(message, ct);

        return due.Count;
    }

    private async Task DispatchOneAsync(OutboundMessage message, CancellationToken ct)
    {
        // Rendered here, at send time (not at creation), so history reflects the
        // template as it stood at the moment of send (BR-013).
        message.RenderedBody = await RenderAsync(message, ct);
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
