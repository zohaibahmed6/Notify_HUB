using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Api.TaskForwarding.Dtos;
using NotifyHub.Api.Tasks.Dtos;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Users;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// P9-10: self-service forwarding rule CRUD (TaskForwardingRulesController) + the new-task-
/// creation resolution it feeds (FallbackUserResolver.ResolveNewTaskAssigneeAsync,
/// ThreadsController.CreateTask).
public class TaskForwardingRulesTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_RejectsSelfAsTarget()
    {
        var (client, callerId) = await _client.AsStaffAsync();

        var response = await client.PostAsJsonAsync("/api/task-forwarding-rules", new TaskForwardingRuleRequest { TargetUserId = callerId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsInactiveTarget()
    {
        long inactiveUserId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var inactive = new User { Username = "forward-target-inactive-9101", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Inactive };
            db.Users.Add(inactive);
            await db.SaveChangesAsync();
            inactiveUserId = inactive.Id;
        }

        var (client, _) = await _client.AsStaffAsync();
        var response = await client.PostAsJsonAsync("/api/task-forwarding-rules", new TaskForwardingRuleRequest { TargetUserId = inactiveUserId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_RejectsOverlappingRange_ForSameUser()
    {
        long targetId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var target = new User { Username = "forward-target-9102", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Active };
            db.Users.Add(target);
            await db.SaveChangesAsync();
            targetId = target.Id;
        }

        var (client, _) = await _client.AsStaffAsync();
        var from = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 8, 10, 0, 0, 0, DateTimeKind.Utc);

        var first = await client.PostAsJsonAsync("/api/task-forwarding-rules", new TaskForwardingRuleRequest { TargetUserId = targetId, From = from, To = to });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Overlaps the first rule's window by 5 days.
        var overlapping = await client.PostAsJsonAsync("/api/task-forwarding-rules",
            new TaskForwardingRuleRequest { TargetUserId = targetId, From = from.AddDays(5), To = to.AddDays(5) });
        Assert.Equal(HttpStatusCode.Conflict, overlapping.StatusCode);

        // Does not overlap — starts the day after the first ends.
        var nonOverlapping = await client.PostAsJsonAsync("/api/task-forwarding-rules",
            new TaskForwardingRuleRequest { TargetUserId = targetId, From = to.AddDays(1), To = to.AddDays(5) });
        Assert.Equal(HttpStatusCode.Created, nonOverlapping.StatusCode);
    }

    [Fact]
    public async Task Delete_OnlyAffectsCallersOwnRule()
    {
        long targetId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
            var target = new User { Username = "forward-target-9103", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Active };
            db.Users.Add(target);
            await db.SaveChangesAsync();
            targetId = target.Id;
        }

        var (staffClient, _) = await _client.AsStaffAsync();
        // Explicit, test-unique date window rather than open-ended (null/null) — the
        // seeded "staff" account is shared across every test in this class (same DB via
        // IClassFixture), and an open-ended rule overlaps literally any other rule that
        // account might already have from a different test.
        var created = await staffClient.PostAsJsonAsync("/api/task-forwarding-rules", new TaskForwardingRuleRequest
        {
            TargetUserId = targetId,
            From = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            To = new DateTime(2020, 1, 2, 0, 0, 0, DateTimeKind.Utc),
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var rule = await created.Content.ReadFromJsonAsync<TaskForwardingRuleDto>();

        var (adminClient, _) = await factory.CreateClient().AsAdminAsync();
        var deleteByAdmin = await adminClient.DeleteAsync($"/api/task-forwarding-rules/{rule!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteByAdmin.StatusCode); // not the owner

        var deleteByOwner = await staffClient.DeleteAsync($"/api/task-forwarding-rules/{rule.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteByOwner.StatusCode);
    }

    [Fact]
    public async Task ResolveNewTaskAssigneeAsync_ActiveNaturalAssignee_IsUsedDirectly_NoForwardingLookup()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var active = new User { Username = "resolve-active-9104", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Active };
        db.Users.Add(active);
        await db.SaveChangesAsync();

        var resolved = await FallbackUserResolver.ResolveNewTaskAssigneeAsync(db, active.Id, CancellationToken.None);

        Assert.Equal(active.Id, resolved);
    }

    [Fact]
    public async Task ResolveNewTaskAssigneeAsync_InactiveWithValidRule_UsesRuleTarget()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var inactive = new User { Username = "resolve-inactive-9105", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Inactive };
        var target = new User { Username = "resolve-target-9105", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Active };
        db.Users.AddRange(inactive, target);
        await db.SaveChangesAsync();

        db.TaskForwardingRules.Add(new TaskForwardingRule { UserId = inactive.Id, TargetUserId = target.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var resolved = await FallbackUserResolver.ResolveNewTaskAssigneeAsync(db, inactive.Id, CancellationToken.None);

        Assert.Equal(target.Id, resolved);
    }

    [Fact]
    public async Task ResolveNewTaskAssigneeAsync_InactiveWithRuleTargetingNowInactiveUser_FallsBackToAdmin()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var inactive = new User { Username = "resolve-inactive-9106", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Inactive };
        // Rule 6: the rule's target has since gone Inactive too — must be ignored, fall to Admin.
        var staleTarget = new User { Username = "resolve-stale-target-9106", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Inactive };
        db.Users.AddRange(inactive, staleTarget);
        await db.SaveChangesAsync();

        db.TaskForwardingRules.Add(new TaskForwardingRule { UserId = inactive.Id, TargetUserId = staleTarget.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var resolved = await FallbackUserResolver.ResolveNewTaskAssigneeAsync(db, inactive.Id, CancellationToken.None);

        var fallbackAdminId = await FallbackUserResolver.ResolveFallbackAdminIdAsync(db, CancellationToken.None);
        Assert.Equal(fallbackAdminId, resolved);
        Assert.NotEqual(staleTarget.Id, resolved);
    }

    [Fact]
    public async Task ResolveNewTaskAssigneeAsync_InactiveWithNoRule_FallsBackToAdmin_UnchangedBehavior()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var inactive = new User { Username = "resolve-inactive-9107", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Inactive };
        db.Users.Add(inactive);
        await db.SaveChangesAsync();

        var resolved = await FallbackUserResolver.ResolveNewTaskAssigneeAsync(db, inactive.Id, CancellationToken.None);
        var fallbackAdminId = await FallbackUserResolver.ResolveFallbackAdminIdAsync(db, CancellationToken.None);

        Assert.Equal(fallbackAdminId, resolved);
    }

    [Fact]
    public async Task CreateTask_ForwardsToRuleTarget_WhenNaturalAssigneeInactive_AndAudits()
    {
        long threadId, targetId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

            var patient = new Patient { Name = "P9-10 Test Patient", Phone = "+19990009001" };
            db.Patients.Add(patient);
            await db.SaveChangesAsync();

            var inactiveOwner = new User { Username = "createtask-inactive-9108", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Inactive };
            var target = new User { Username = "createtask-target-9108", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Active };
            db.Users.AddRange(inactiveOwner, target);
            await db.SaveChangesAsync();
            targetId = target.Id;

            var thread = new ConversationThread { PatientId = patient.Id, UnreadCount = 0, AssignedStaffId = inactiveOwner.Id };
            db.Threads.Add(thread);
            await db.SaveChangesAsync();
            threadId = thread.Id;

            db.TaskForwardingRules.Add(new TaskForwardingRule { UserId = inactiveOwner.Id, TargetUserId = target.Id, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        var (client, _) = await _client.AsStaffAsync();
        var response = await client.PostAsJsonAsync($"/api/threads/{threadId}/tasks", new CreateTaskRequest());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<TaskDto>();

        using var assertScope = factory.Services.CreateScope();
        var assertDb = assertScope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var task = await assertDb.Tasks.SingleAsync(t => t.Id == created!.Id);

        Assert.Equal(targetId, task.AssignedStaffId);

        var audit = await assertDb.AuditLogs.SingleAsync(a => a.EntityType == "TaskItem" && a.EntityId == task.Id && a.Action == "forward");
        Assert.Equal("system", audit.Actor);
    }
}
