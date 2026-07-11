using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Escalation;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// FR-008/BR-004: the escalation job isn't hosted inside WebApplicationFactory (that's
/// the Worker process) — instantiated directly against the same DbContext, same pattern
/// OutboundPipelineTests uses to simulate the dispatcher's claim step.
public class EscalationJobTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task EscalateOverdueTasksAsync_FlagsOverdueTask_AndReassignsToAdmin()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var (staff, admin) = await SeedStaffAndAdminAsync(db, phoneSuffix: "2001");
        var task = await CreateOverdueTaskAsync(db, staff.Id, phone: "+19990002001");

        var job = new EscalationJob(db, NullLogger<EscalationJob>.Instance);
        var count = await job.EscalateOverdueTasksAsync(CancellationToken.None);

        Assert.True(count >= 1);

        var updated = await db.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal(NotifyHubTaskStatus.Escalated, updated.Status);
        Assert.Equal(staff.Id, updated.OriginalOwnerId); // BR-007d: original owner untouched

        var escalationAudit = await db.AuditLogs.SingleAsync(a => a.EntityType == "TaskItem" && a.EntityId == task.Id && a.Action == "escalation");
        Assert.Equal("system", escalationAudit.Actor);
    }

    [Fact]
    public async Task EscalateOverdueTasksAsync_DoesNotReescalate_AlreadyEscalatedTask()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var (staff, _) = await SeedStaffAndAdminAsync(db, phoneSuffix: "2002");
        var task = await CreateOverdueTaskAsync(db, staff.Id, phone: "+19990002002", status: NotifyHubTaskStatus.Escalated);

        var job = new EscalationJob(db, NullLogger<EscalationJob>.Instance);
        await job.EscalateOverdueTasksAsync(CancellationToken.None);

        var escalationAuditCount = await db.AuditLogs.CountAsync(a => a.EntityType == "TaskItem" && a.EntityId == task.Id && a.Action == "escalation");
        Assert.Equal(0, escalationAuditCount);
    }

    private static async Task<(User Staff, User Admin)> SeedStaffAndAdminAsync(NotifyHubDbContext db, string phoneSuffix)
    {
        var staff = new User { Username = $"escalation-staff-{phoneSuffix}", PasswordHash = "unused", Role = UserRole.Staff };
        var admin = new User { Username = $"escalation-admin-{phoneSuffix}", PasswordHash = "unused", Role = UserRole.Admin };
        db.Users.AddRange(staff, admin);
        await db.SaveChangesAsync();
        return (staff, admin);
    }

    private static async Task<TaskItem> CreateOverdueTaskAsync(
        NotifyHubDbContext db, long ownerId, string phone, NotifyHubTaskStatus status = NotifyHubTaskStatus.Open)
    {
        var patient = new Patient { Name = $"Test Patient {phone}", Phone = phone };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var thread = new ConversationThread { PatientId = patient.Id };
        db.Threads.Add(thread);
        await db.SaveChangesAsync();

        var task = new TaskItem
        {
            ThreadId = thread.Id,
            Priority = TaskPriority.Medium,
            DueAt = DateTime.UtcNow.AddDays(-1),
            Status = status,
            AssignedStaffId = ownerId,
            OriginalOwnerId = ownerId,
            OccurrenceCount = 1,
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        return task;
    }
}
