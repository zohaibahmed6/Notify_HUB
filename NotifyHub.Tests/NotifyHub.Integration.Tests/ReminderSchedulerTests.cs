using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Reminders;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// FR-009/BR-003/BR-010: the reminder scheduler isn't hosted inside WebApplicationFactory
/// (that's the Worker process) — instantiated directly against the same DbContext, same
/// pattern EscalationJobTests uses for EscalationJob.
public class ReminderSchedulerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task RunAsync_QueuesDueReminder_AndReRunning_DoesNotDuplicate()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var template48h = await Get48hTemplateAsync(db);
        var appointment = await CreateAppointmentAsync(db, phoneSuffix: "3001", scheduledInHours: 47); // inside the 48h window

        var scheduler = new ReminderScheduler(db, NullLogger<ReminderScheduler>.Instance);
        await scheduler.RunAsync(CancellationToken.None);
        await scheduler.RunAsync(CancellationToken.None); // BR-003: re-running must not duplicate

        var reminders = await RemindersForAppointmentAsync(db, appointment.Id, template48h.Id);

        var reminder = Assert.Single(reminders);
        Assert.Equal(MessageStatus.Queued, reminder.Status);
        Assert.Equal(SenderType.System, reminder.SenderType);
        Assert.Equal(appointment.PatientId, reminder.PatientId);
        Assert.NotNull(reminder.IdempotencyKey);
    }

    [Fact]
    public async Task RunAsync_NotYetDueAppointment_QueuesNothing()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var template48h = await Get48hTemplateAsync(db);
        var appointment = await CreateAppointmentAsync(db, phoneSuffix: "3002", scheduledInHours: 72); // outside the 48h window

        var scheduler = new ReminderScheduler(db, NullLogger<ReminderScheduler>.Instance);
        await scheduler.RunAsync(CancellationToken.None);

        var reminders = await RemindersForAppointmentAsync(db, appointment.Id, template48h.Id);
        Assert.Empty(reminders);
    }

    [Fact]
    public async Task RunAsync_AfterReschedule_SupersedesOldQueuedReminder_AndQueuesFreshOne()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var template48h = await Get48hTemplateAsync(db);
        var appointment = await CreateAppointmentAsync(db, phoneSuffix: "3003", scheduledInHours: 47);

        var scheduler = new ReminderScheduler(db, NullLogger<ReminderScheduler>.Instance);
        await scheduler.RunAsync(CancellationToken.None);

        var originalReminder = Assert.Single(await RemindersForAppointmentAsync(db, appointment.Id, template48h.Id));
        var originalTriggerReference = originalReminder.TriggerReference;

        // Simulate a reschedule (no appointment-management endpoint exists yet — §7/§14 —
        // appointments are stub data; this mutates scheduled_at directly, same as any
        // future reschedule path would) to a time still inside the 48h window.
        appointment.ScheduledAt = DateTime.UtcNow.AddHours(30);
        await db.SaveChangesAsync();

        await scheduler.RunAsync(CancellationToken.None);

        var afterReschedule = await RemindersForAppointmentAsync(db, appointment.Id, template48h.Id);
        Assert.Equal(2, afterReschedule.Count);

        var superseded = Assert.Single(afterReschedule, m => m.TriggerReference == originalTriggerReference);
        Assert.Equal(MessageStatus.Superseded, superseded.Status);

        var fresh = Assert.Single(afterReschedule, m => m.TriggerReference != originalTriggerReference);
        Assert.Equal(MessageStatus.Queued, fresh.Status);

        var supersedeAudit = await db.AuditLogs.SingleAsync(a =>
            a.EntityType == "OutboundMessage" && a.EntityId == superseded.Id && a.Action == "superseded");
        Assert.Equal("system", supersedeAudit.Actor);

        // BR-003 still holds post-reschedule: re-running again must not duplicate the fresh one.
        await scheduler.RunAsync(CancellationToken.None);
        Assert.Equal(2, (await RemindersForAppointmentAsync(db, appointment.Id, template48h.Id)).Count);
    }

    private static async Task<MessageTemplate> Get48hTemplateAsync(NotifyHubDbContext db) =>
        await db.MessageTemplates.SingleAsync(t => t.TriggerType == TriggerType.AppointmentReminder && t.OffsetHours == 48);

    private static async Task<Appointment> CreateAppointmentAsync(NotifyHubDbContext db, string phoneSuffix, int scheduledInHours)
    {
        var patient = new Patient { Name = $"Reminder Test Patient {phoneSuffix}", Phone = $"+1888000{phoneSuffix}" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var appointment = new Appointment
        {
            PatientId = patient.Id,
            ScheduledAt = DateTime.UtcNow.AddHours(scheduledInHours),
            Status = AppointmentStatus.Scheduled,
        };
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        return appointment;
    }

    private static async Task<List<OutboundMessage>> RemindersForAppointmentAsync(NotifyHubDbContext db, long appointmentId, long templateId)
    {
        var prefix = $"appointment:{appointmentId}:reminder:";
        return await db.OutboundMessages
            .Where(m => m.TemplateId == templateId && m.TriggerReference != null && m.TriggerReference.StartsWith(prefix))
            .ToListAsync();
    }
}
