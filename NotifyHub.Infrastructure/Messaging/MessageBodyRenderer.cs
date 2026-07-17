using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Messaging;

/// Shared merge-field rendering, extracted so both MessageDispatcher (send-time render) and
/// TemplatesController (edit-time re-render, P9-05 net #1) resolve {{appointment_time}} the
/// same way instead of maintaining two copies of the TriggerReference/EventTime branch.
public class MessageBodyRenderer(NotifyHubDbContext db)
{
    public async Task<string> RenderAsync(OutboundMessage message, string templateBody, CancellationToken ct)
    {
        var fields = new Dictionary<string, string>
        {
            ["patient_name"] = message.Patient.Name,
        };

        // trigger_reference encodes the business event (BR-009), e.g. "appointment:{id}:created" —
        // parsed here to resolve {{appointment_time}} for appointment-reminder templates.
        // Reminder SMS (P9-08, rule 34) carries EventTime instead and never sets TriggerReference,
        // so the two branches are mutually exclusive on any given OutboundMessage.
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
        else if (message.EventTime is { } eventTime)
        {
            fields["appointment_time"] = eventTime.ToString("u");
        }

        return TemplateRenderer.Render(templateBody, fields);
    }
}
