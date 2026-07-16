using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Api.Common;
using NotifyHub.Api.Tasks.Dtos;
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

    [Fact]
    public async Task List_DefaultsToActiveOnly()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var activeTask = await CreateTaskAsync("+19990001009", staffId, DateTime.UtcNow.AddDays(1));
        var inactiveTask = await CreateTaskAsync("+19990001010", staffId, DateTime.UtcNow.AddDays(1));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var t = await db.Tasks.SingleAsync(x => x.Id == inactiveTask.Id);
            t.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await client.GetFromJsonAsync<PagedResultShape>($"/api/tasks?pageSize=100");

        Assert.Contains(response!.Items, t => t.Id == activeTask.Id);
        Assert.DoesNotContain(response.Items, t => t.Id == inactiveTask.Id);
    }

    [Fact]
    public async Task List_IsActiveFalse_ReturnsOnlyInactiveTasks()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var task = await CreateTaskAsync("+19990001011", staffId, DateTime.UtcNow.AddDays(1));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var t = await db.Tasks.SingleAsync(x => x.Id == task.Id);
            t.IsActive = false;
            await db.SaveChangesAsync();
        }

        var response = await client.GetFromJsonAsync<PagedResultShape>($"/api/tasks?isActive=false&pageSize=100");

        Assert.Contains(response!.Items, t => t.Id == task.Id);
    }

    [Fact]
    public async Task List_FiltersByDescriptionAndDueDateRange()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var dueAt = new DateTime(2026, 3, 1, 9, 0, 0, DateTimeKind.Utc);
        var task = await CreateTaskAsync("+19990001012", staffId, dueAt);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var t = await db.Tasks.SingleAsync(x => x.Id == task.Id);
            t.Description = "Patient requested a callback about repeat prescription";
            await db.SaveChangesAsync();
        }

        var match = await client.GetFromJsonAsync<PagedResultShape>(
            $"/api/tasks?description=repeat prescription&dueFrom=2026-02-28T00:00:00Z&dueTo=2026-03-02T00:00:00Z");
        Assert.Contains(match!.Items, t => t.Id == task.Id);

        var noMatch = await client.GetFromJsonAsync<PagedResultShape>("/api/tasks?description=nonexistent-text-xyz");
        Assert.DoesNotContain(noMatch!.Items, t => t.Id == task.Id);
    }

    [Fact]
    public async Task CreateTask_AutoPopulatesDescriptionFromLastMessage()
    {
        var (client, staffId) = await _client.AsStaffAsync();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = new Patient { Name = "Description Auto-populate Patient", Phone = "+19990001013" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var thread = new ConversationThread { PatientId = patient.Id, AssignedStaffId = staffId };
        db.Threads.Add(thread);
        await db.SaveChangesAsync();

        db.InboundMessages.Add(new InboundMessage { ThreadId = thread.Id, Body = "Older inbound message", ReceivedAt = DateTime.UtcNow.AddMinutes(-10) });
        db.OutboundMessages.Add(new OutboundMessage
        {
            PatientId = patient.Id,
            ThreadId = thread.Id,
            SenderType = SenderType.Staff,
            RenderedBody = "Newest outbound reply",
            CreatedAt = DateTime.UtcNow,
            Status = MessageStatus.Queued,
            AttemptCount = 0,
        });
        await db.SaveChangesAsync();

        var response = await client.PostAsJsonAsync($"/api/threads/{thread.Id}/tasks", new { });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var dto = await response.Content.ReadFromJsonAsync<TaskDtoShape>();
        Assert.Equal("Newest outbound reply", dto!.Description);
        Assert.Equal("General", dto.TaskType);
        Assert.True(dto.IsActive);
    }

    private class PagedResultShape
    {
        public List<TaskDtoShape> Items { get; set; } = [];
    }

    private class TaskDtoShape
    {
        public long Id { get; set; }
        public string? Description { get; set; }
        public string TaskType { get; set; } = default!;
        public bool IsActive { get; set; }
    }

    [Fact]
    public async Task List_And_Detail_IncludePatientName()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var task = await CreateTaskAsync("+19990001013", staffId, DateTime.UtcNow.AddDays(1));

        var listResponse = await client.GetAsync("/api/tasks?isActive=true&pageSize=100");
        listResponse.EnsureSuccessStatusCode();
        var listResult = await listResponse.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();
        Assert.Contains(listResult!.Items, t => t.Id == task.Id && t.PatientName == "Test Patient +19990001013");

        var detailResponse = await client.GetAsync($"/api/tasks/{task.Id}");
        detailResponse.EnsureSuccessStatusCode();
        var detail = await detailResponse.Content.ReadFromJsonAsync<TaskDto>();
        Assert.Equal("Test Patient +19990001013", detail!.PatientName);
    }

    [Fact]
    public async Task List_SortByPriority_OrdersBySeverityNotAlphabetically()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var due = DateTime.UtcNow.AddDays(1);
        var urgent = await CreateTaskAsync("+19990001014", staffId, due, priority: TaskPriority.Urgent);
        var low = await CreateTaskAsync("+19990001015", staffId, due, priority: TaskPriority.Low);
        var high = await CreateTaskAsync("+19990001016", staffId, due, priority: TaskPriority.High);

        var response = await client.GetAsync("/api/tasks?sortBy=priority&pageSize=100");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();

        var ids = result!.Items.Select(t => t.Id).ToList();
        // Alphabetical would give High, Low, Medium, Urgent — severity-ascending must give
        // Low before High before Urgent instead.
        Assert.True(ids.IndexOf(low.Id) < ids.IndexOf(high.Id));
        Assert.True(ids.IndexOf(high.Id) < ids.IndexOf(urgent.Id));
    }

    [Fact]
    public async Task List_SortByStatusDesc_UsesStatusRankOrder()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var due = DateTime.UtcNow.AddDays(1);
        var open = await CreateTaskAsync("+19990001017", staffId, due, status: NotifyHubTaskStatus.Open);
        var completed = await CreateTaskAsync("+19990001018", staffId, due, status: NotifyHubTaskStatus.Completed);

        var response = await client.GetAsync("/api/tasks?sortBy=status&sortDir=desc&pageSize=100");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();

        var ids = result!.Items.Select(t => t.Id).ToList();
        // Rank order matches the Board's Kanban columns (Open, InProgress, Escalated,
        // Completed, Cancelled) — descending puts the highest-rank (Completed) first.
        Assert.True(ids.IndexOf(completed.Id) < ids.IndexOf(open.Id));
    }

    [Fact]
    public async Task List_UnassignedTrue_ReturnsOnlyNullAssignee()
    {
        var (client, staffId) = await _client.AsStaffAsync();
        var due = DateTime.UtcNow.AddDays(1);
        var assigned = await CreateTaskAsync("+19990001019", staffId, due);
        var unassigned = await CreateTaskAsync("+19990001020", staffId, due, assignedStaffId: null);

        var response = await client.GetAsync("/api/tasks?unassigned=true&pageSize=100");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();

        var ids = result!.Items.Select(t => t.Id).ToList();
        Assert.Contains(unassigned.Id, ids);
        Assert.DoesNotContain(assigned.Id, ids);
    }

    [Fact]
    public async Task List_PriorityFilter_InvalidValue_Returns400()
    {
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.GetAsync("/api/tasks?priority=NotAPriority");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<TaskItem> CreateTaskAsync(
        string phone,
        long ownerId,
        DateTime dueAt,
        bool isRecurring = false,
        int? recurrenceIntervalDays = null,
        int? recurrenceMaxOccurrences = null,
        NotifyHubTaskStatus status = NotifyHubTaskStatus.Open,
        TaskPriority priority = TaskPriority.Medium,
        long? assignedStaffId = -1)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var patient = new Patient { Name = $"Test Patient {phone}", Phone = phone };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var thread = new ConversationThread { PatientId = patient.Id };
        db.Threads.Add(thread);
        await db.SaveChangesAsync();

        // -1 is a sentinel meaning "default to ownerId" (distinct from an explicit null,
        // which callers pass to test an unassigned task).
        var task = new TaskItem
        {
            ThreadId = thread.Id,
            Priority = priority,
            DueAt = dueAt,
            Status = status,
            AssignedStaffId = assignedStaffId == -1 ? ownerId : assignedStaffId,
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
