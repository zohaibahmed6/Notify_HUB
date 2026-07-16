using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// One template per FR-009 reminder offset (48h/2h) plus one each for the other two
/// original trigger scenarios (FR-001: at least 3 trigger types functional, historically —
/// TriggerType itself was removed as vestigial, templates are now selected by id/name).
/// Idempotent: skips entirely if any template already exists.
public class TemplateSeedStep : IDbSeedStep
{
    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        if (await db.MessageTemplates.AnyAsync(ct))
            return;

        db.MessageTemplates.AddRange(
            new MessageTemplate
            {
                Name = "Appointment Reminder (48h)",
                Body = "Hi {{patient_name}}, reminder: your appointment is at {{appointment_time}}.",
                OffsetHours = 48,
            },
            new MessageTemplate
            {
                Name = "Appointment Reminder (2h)",
                Body = "Hi {{patient_name}}, this is a reminder your appointment is at {{appointment_time}} — see you soon!",
                OffsetHours = 2,
            },
            new MessageTemplate
            {
                Name = "Medication Alert",
                Body = "Hi {{patient_name}}, this is a reminder to take your medication.",
                OffsetHours = 1,
            },
            new MessageTemplate
            {
                Name = "Prescription Alert",
                Body = "Hi {{patient_name}}, your prescription is ready for pickup.",
                OffsetHours = 1,
            });

        await db.SaveChangesAsync(ct);
    }
}
