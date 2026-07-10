using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// Queues a handful of demo messages so the dispatcher has work to do as soon as the
/// stack comes up (no reminder scheduler/UI exists yet to create these — both land in
/// later steps). Idempotent: skips entirely if any outbound message already exists.
/// Must run after PatientAppointmentSeedStep and TemplateSeedStep (DI registration order).
public class DemoOutboundMessageSeedStep : IDbSeedStep
{
    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        if (await db.OutboundMessages.AnyAsync(ct))
            return;

        var patients = await db.Patients.Include(p => p.Appointments).OrderBy(p => p.Id).ToListAsync(ct);
        var templates = await db.MessageTemplates.ToListAsync(ct);
        if (patients.Count == 0 || templates.Count == 0)
            return;

        var appointmentReminder = templates.First(t => t.TriggerType == TriggerType.AppointmentReminder && t.OffsetHours == 48);
        var medicationAlert = templates.First(t => t.TriggerType == TriggerType.MedicationAlert);
        var prescriptionAlert = templates.First(t => t.TriggerType == TriggerType.PrescriptionAlert);

        var now = DateTime.UtcNow;
        var messages = new List<OutboundMessage>();

        foreach (var patient in patients.Take(5))
        {
            var appointment = patient.Appointments.First();
            var triggerReference = $"appointment:{appointment.Id}:created";
            messages.Add(BuildMessage(patient, appointmentReminder, triggerReference, now));
        }

        foreach (var patient in patients.Skip(5).Take(3))
        {
            var triggerReference = $"medication:{patient.Id}:seed";
            messages.Add(BuildMessage(patient, medicationAlert, triggerReference, now));
        }

        foreach (var patient in patients.Skip(8).Take(2))
        {
            var triggerReference = $"prescription:{patient.Id}:seed";
            messages.Add(BuildMessage(patient, prescriptionAlert, triggerReference, now));
        }

        db.OutboundMessages.AddRange(messages);
        await db.SaveChangesAsync(ct);
    }

    private static OutboundMessage BuildMessage(Patient patient, MessageTemplate template, string triggerReference, DateTime now) =>
        new()
        {
            PatientId = patient.Id,
            TemplateId = template.Id,
            SenderType = SenderType.System,
            TriggerReference = triggerReference,
            CreatedAt = now,
            Status = MessageStatus.Queued,
            IdempotencyKey = IdempotencyKeyGenerator.Generate(patient.Id, template.Id, triggerReference),
            AttemptCount = 0,
        };
}
