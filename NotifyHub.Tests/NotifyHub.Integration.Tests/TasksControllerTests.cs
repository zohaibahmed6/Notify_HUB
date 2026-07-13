using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// FR-008/BR-007/BR-014: task update, recurrence spawn on completion, and escalated-status
/// auto-revert. Each test creates its own thread/task directly via DbContext.
public class TasksControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Update_CompletingRecurringTask_SpawnsNextOccurrence()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var dueAt = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        var task = await CreateTaskAsync("+19990001001", staffId, dueAt, isRecurring: true, recurrenceIntervalDays: 7);

        var response = await client.PatchAsJsonAsync($"/api/tasks/{task.Id}", new { status = "Completed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var completed = await db.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal(NotifyHubTaskStatus.Completed, completed.Status);

        var next = await db.Tasks.SingleAsync(t => t.ThreadId == task.ThreadId && t.Id != task.Id);
        Assert.Equal(dueAt.AddDays(7), next.DueAt);
        Assert.Equal(2, next.OccurrenceCount);
        Assert.Equal(staffId, next.OriginalOwnerId);
        Assert.Equal(staffId, next.AssignedStaffId);
        Assert.Equal(NotifyHubTaskStatus.Open, next.Status);
    }

    [Fact]
    public async Task Update_CompletingNonRecurringTask_DoesNotSpawnNext()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var task = await CreateTaskAsync("+19990001002", staffId, DateTime.UtcNow.AddDays(1), isRecurring: false);

        await client.PatchAsJsonAsync($"/api/tasks/{task.Id}", new { status = "Completed" });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var count = await db.Tasks.CountAsync(t => t.ThreadId == task.ThreadId);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Update_RecurringTask_PastMaxOccurrences_DoesNotSpawnNext()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var task = await CreateTaskAsync(
            "+19990001003", staffId, DateTime.UtcNow, isRecurring: true, recurrenceIntervalDays: 7, recurrenceMaxOccurrences: 1);

        await client.PatchAsJsonAsync($"/api/tasks/{task.Id}", new { status = "Completed" });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var count = await db.Tasks.CountAsync(t => t.ThreadId == task.ThreadId);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Update_EscalatedTask_AutoRevertsToInProgress_WhenAssigneeTakesAction()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var task = await CreateTaskAsync("+19990001004", staffId, DateTime.UtcNow.AddDays(-1), status: NotifyHubTaskStatus.Escalated);

        // No explicit status field — just a priority change (BR-014: "any action taken").
        var response = await client.PatchAsJsonAsync($"/api/tasks/{task.Id}", new { priority = "High" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var updated = await db.Tasks.SingleAsync(t => t.Id == task.Id);

        Assert.Equal(NotifyHubTaskStatus.InProgress, updated.Status);
    }

    [Fact]
    public async Task Update_EscalatedTask_DoesNotAutoRevert_WhenActorIsNotCurrentAssignee()
    {
        var (staffClient, staffId) = await _client.AsStaffAsync();
        var task = await CreateTaskAsync("+19990001005", staffId, DateTime.UtcNow.AddDays(-1), status: NotifyHubTaskStatus.Escalated);

        var (adminClient, _) = await factory.CreateClient().AsAdminAsync();
        var response = await adminClient.PatchAsJsonAsync($"/api/tasks/{task.Id}", new { priority = "High" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var updated = await db.Tasks.SingleAsync(t => t.Id == task.Id);

        Assert.Equal(NotifyHubTaskStatus.Escalated, updated.Status);
    }

    [Fact]
    public async Task Forward_ToActiveUser_ReassignsAndAudits()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var task = await CreateTaskAsync("+19990001006", staffId, DateTime.UtcNow.AddDays(1));

        var (_, adminId) = await factory.CreateClient().AsAdminAsync();

        var response = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/forward", new { targetUserId = adminId, note = "please pick this up" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var updated = await db.Tasks.SingleAsync(t => t.Id == task.Id);
        Assert.Equal(adminId, updated.AssignedStaffId);
        Assert.Equal(NotifyHubTaskStatus.Open, updated.Status); // untouched by forwarding

        var audit = await db.AuditLogs.SingleAsync(a => a.EntityType == "TaskItem" && a.EntityId == task.Id && a.Action == "forward");
        Assert.Contains("please pick this up", audit.Detail);
    }

    [Fact]
    public async Task Forward_ToInactiveUser_Returns400()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var inactiveUser = new User { Username = "forward-target-inactive-9007", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Inactive };
        db.Users.Add(inactiveUser);
        await db.SaveChangesAsync();

        var (client, staffId) = await _client.AsStaffAsync();
        var task = await CreateTaskAsync("+19990001007", staffId, DateTime.UtcNow.AddDays(1));

        var response = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/forward", new { targetUserId = inactiveUser.Id });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Forward_EscalatedTask_DoesNotChangeWorkflowStatus()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var task = await CreateTaskAsync("+19990001008", staffId, DateTime.UtcNow.AddDays(-1), status: NotifyHubTaskStatus.Escalated);

        var (_, adminId) = await factory.CreateClient().AsAdminAsync();
        await client.PostAsJsonAsync($"/api/tasks/{task.Id}/forward", new { targetUserId = adminId });

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var updated = await db.Tasks.SingleAsync(t => t.Id == task.Id);

        Assert.Equal(NotifyHubTaskStatus.Escalated, updated.Status);
        Assert.Equal(adminId, updated.AssignedStaffId);
    }

    private async Task<TaskItem> CreateTaskAsync(
        string phone,
        long ownerId,
        DateTime dueAt,
        bool isRecurring = false,
        int? recurrenceIntervalDays = null,
        int? recurrenceMaxOccurrences = null,
        NotifyHubTaskStatus status = NotifyHubTaskStatus.Open)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

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
            DueAt = dueAt,
            Status = status,
            AssignedStaffId = ownerId,
            OriginalOwnerId = ownerId,
            IsRecurring = isRecurring,
            RecurrenceIntervalDays = recurrenceIntervalDays,
            RecurrenceMaxOccurrences = recurrenceMaxOccurrences,
            OccurrenceCount = 1,
        };
        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        return task;
    }
}
