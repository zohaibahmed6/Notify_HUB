using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// One template per FR-009 reminder offset (48h/2h) plus one each for the other two
/// trigger types (FR-001: at least 3 trigger types functional). Idempotent: skips
/// entirely if any template already exists.
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
                TriggerType = TriggerType.AppointmentReminder,
                OffsetHours = 48,
            },
            new MessageTemplate
            {
                Name = "Appointment Reminder (2h)",
                Body = "Hi {{patient_name}}, this is a reminder your appointment is at {{appointment_time}} — see you soon!",
                TriggerType = TriggerType.AppointmentReminder,
                OffsetHours = 2,
            },
            new MessageTemplate
            {
                Name = "Medication Alert",
                Body = "Hi {{patient_name}}, this is a reminder to take your medication.",
                TriggerType = TriggerType.MedicationAlert,
                OffsetHours = 1,
            },
            new MessageTemplate
            {
                Name = "Prescription Alert",
                Body = "Hi {{patient_name}}, your prescription is ready for pickup.",
                TriggerType = TriggerType.PrescriptionAlert,
                OffsetHours = 1,
            });

        await db.SaveChangesAsync(ct);
    }
}
