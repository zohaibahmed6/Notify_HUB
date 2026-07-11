using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Auditing;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Reminders;

/// FR-009/BR-003/BR-010: polls appointments due for a 48h or 2h reminder and queues
/// them onto outbound_messages — the same table/dispatcher pipeline as every other
/// message (Worker's DispatcherWorker/MessageDispatcher picks them up unchanged, no
/// parallel send mechanism). Kept separate from the hosting BackgroundService loop
/// (ReminderWorker) so it's directly testable, mirroring MessageDispatcher/EscalationJob.
public class ReminderScheduler(NotifyHubDbContext db, ILogger<ReminderScheduler> logger)
{
    public async Task RunAsync(CancellationToken ct)
    {
        var reminderTemplates = await db.MessageTemplates
            .Where(t => t.TriggerType == TriggerType.AppointmentReminder)
            .ToListAsync(ct);

        if (reminderTemplates.Count == 0)
            return;

        var now = DateTime.UtcNow;

        var superseded = await SupersedeStaleRemindersAsync(reminderTemplates, ct);
        var created = await CreateDueRemindersAsync(reminderTemplates, now, ct);

        if (superseded > 0 || created > 0)
            logger.LogInformation("Reminder scheduler: superseded {Superseded}, created {Created}", superseded, created);
    }

    /// BR-010: a queued reminder whose trigger_reference embeds a ScheduledAt that no
    /// longer matches the appointment's current ScheduledAt was queued for a time that
    /// no longer holds — the appointment was rescheduled (or removed) since. Mark it
    /// superseded so the dispatcher never sends it; CreateDueRemindersAsync queues a
    /// fresh one under the new trigger_reference if/when it's due.
    private async Task<int> SupersedeStaleRemindersAsync(List<MessageTemplate> reminderTemplates, CancellationToken ct)
    {
        var templateIds = reminderTemplates.Select(t => t.Id).ToHashSet();

        var queuedReminders = await db.OutboundMessages
            .Where(m => m.Status == MessageStatus.Queued
                && m.TemplateId != null && templateIds.Contains(m.TemplateId.Value)
                && m.TriggerReference != null)
            .ToListAsync(ct);

        if (queuedReminders.Count == 0)
            return 0;

        var parsed = queuedReminders
            .Select(m => (Message: m, Parsed: ReminderTriggerReference.TryParse(m.TriggerReference!, out var appointmentId, out _, out var ticks), appointmentId, ticks))
            .Where(x => x.Parsed)
            .ToList();

        if (parsed.Count == 0)
            return 0;

        var appointmentIds = parsed.Select(x => x.appointmentId).ToHashSet();
        var appointments = await db.Appointments
            .Where(a => appointmentIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        var supersededCount = 0;
        foreach (var (message, _, appointmentId, ticks) in parsed)
        {
            // Appointment deleted, or rescheduled (ScheduledAt no longer matches what
            // this reminder was queued for).
            if (!appointments.TryGetValue(appointmentId, out var appointment) || appointment.ScheduledAt.Ticks != ticks)
            {
                message.Status = MessageStatus.Superseded;
                message.NextRetryAt = null;
                AuditLogger.Add(db, actor: "system", action: "superseded", entityType: "OutboundMessage", entityId: message.Id,
                    detail: "appointment rescheduled or removed; reminder no longer applies");
                supersededCount++;
            }
        }

        if (supersededCount > 0)
            await db.SaveChangesAsync(ct);

        return supersededCount;
    }

    /// FR-009/BR-003: for every upcoming, non-cancelled appointment whose 48h or 2h
    /// reminder window has opened, queue the reminder if one doesn't already exist —
    /// re-running this finds the existing row by idempotency key and creates nothing.
    private async Task<int> CreateDueRemindersAsync(List<MessageTemplate> reminderTemplates, DateTime now, CancellationToken ct)
    {
        var maxOffsetHours = reminderTemplates.Max(t => t.OffsetHours);

        // Widest possible window across all offsets — a valid pre-filter since IsDue's
        // window for any given offset is always a subset of this one.
        var upcoming = await db.Appointments
            .Where(a => a.Status == AppointmentStatus.Scheduled
                && a.ScheduledAt > now
                && a.ScheduledAt <= now.AddHours(maxOffsetHours))
            .ToListAsync(ct);

        if (upcoming.Count == 0)
            return 0;

        var createdCount = 0;
        foreach (var appointment in upcoming)
        {
            foreach (var template in reminderTemplates)
            {
                if (!ReminderDueCalculator.IsDue(appointment.ScheduledAt, template.OffsetHours, now))
                    continue;

                var triggerReference = ReminderTriggerReference.Build(appointment.Id, template.OffsetHours, appointment.ScheduledAt);
                var idempotencyKey = IdempotencyKeyGenerator.Generate(appointment.PatientId, template.Id, triggerReference);

                var exists = await db.OutboundMessages.AnyAsync(m => m.IdempotencyKey == idempotencyKey, ct);
                if (exists)
                    continue;

                db.OutboundMessages.Add(new OutboundMessage
                {
                    PatientId = appointment.PatientId,
                    ThreadId = null,
                    TemplateId = template.Id,
                    SenderType = SenderType.System,
                    TriggerReference = triggerReference,
                    RenderedBody = null,
                    CreatedAt = now,
                    Status = MessageStatus.Queued,
                    IdempotencyKey = idempotencyKey,
                    AttemptCount = 0,
                });
                createdCount++;
            }
        }

        if (createdCount > 0)
            await db.SaveChangesAsync(ct);

        return createdCount;
    }
}
