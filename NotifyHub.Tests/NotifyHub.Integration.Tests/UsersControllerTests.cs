using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// §7: user creation, assignable-list filtering, and automatic task forwarding when a
/// user transitions to Inactive/OnLeave.
public class UsersControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_AsAdmin_CreatesUser()
    {
        var (client, _) = await _client.AsAdminAsync();

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            username = "new-staff-9001",
            fullName = "New Staff",
            password = "ValidPass1!",
            role = "Staff",
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var created = await db.Users.SingleAsync(u => u.Username == "new-staff-9001");
        Assert.Equal(UserStatus.Active, created.Status);
        Assert.Equal("New Staff", created.FullName);
    }

    [Fact]
    public async Task Create_AsStaff_Forbidden()
    {
        var (client, _) = await _client.AsStaffAsync();

        var response = await client.PostAsJsonAsync("/api/users", new
        {
            username = "should-not-be-created",
            password = "ValidPass1!",
            role = "Staff",
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Assignable_ExcludesInactiveAndOnLeaveUsers()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var active = new User { Username = "assignable-active-9002", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Active };
        var inactive = new User { Username = "assignable-inactive-9002", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Inactive };
        var onLeave = new User { Username = "assignable-onleave-9002", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.OnLeave };
        db.Users.AddRange(active, inactive, onLeave);
        await db.SaveChangesAsync();

        var (client, _) = await _client.AsStaffAsync();
        var users = await client.GetFromJsonAsync<List<UserDtoShape>>("/api/users/assignable");

        Assert.Contains(users!, u => u.Username == "assignable-active-9002");
        Assert.DoesNotContain(users!, u => u.Username == "assignable-inactive-9002");
        Assert.DoesNotContain(users!, u => u.Username == "assignable-onleave-9002");
    }

    [Fact]
    public async Task UpdateStatus_ToInactive_AutoForwardsOpenTasksToFallbackAdmin()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var staff = new User { Username = "forward-staff-9003", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Active };
        db.Users.Add(staff);
        await db.SaveChangesAsync();

        var patient = new Patient { Name = "Forward Test Patient", Phone = "+19990009003" };
        db.Patients.Add(patient);
        await db.SaveChangesAsync();

        var thread = new ConversationThread { PatientId = patient.Id };
        db.Threads.Add(thread);
        await db.SaveChangesAsync();

        var openTask = new TaskItem
        {
            ThreadId = thread.Id,
            Priority = TaskPriority.Medium,
            DueAt = DateTime.UtcNow.AddDays(1),
            Status = NotifyHubTaskStatus.Open,
            AssignedStaffId = staff.Id,
            OriginalOwnerId = staff.Id,
            OccurrenceCount = 1,
        };
        var completedTask = new TaskItem
        {
            ThreadId = thread.Id,
            Priority = TaskPriority.Medium,
            DueAt = DateTime.UtcNow.AddDays(1),
            Status = NotifyHubTaskStatus.Completed,
            AssignedStaffId = staff.Id,
            OriginalOwnerId = staff.Id,
            OccurrenceCount = 1,
        };
        db.Tasks.AddRange(openTask, completedTask);
        await db.SaveChangesAsync();

        var (client, _) = await _client.AsAdminAsync();
        var response = await client.PatchAsJsonAsync($"/api/users/{staff.Id}/status", new { status = "Inactive" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

        var updatedOpen = await verifyDb.Tasks.SingleAsync(t => t.Id == openTask.Id);
        Assert.NotEqual(staff.Id, updatedOpen.AssignedStaffId); // forwarded away
        Assert.NotNull(updatedOpen.AssignedStaffId);

        var updatedCompleted = await verifyDb.Tasks.SingleAsync(t => t.Id == completedTask.Id);
        Assert.Equal(staff.Id, updatedCompleted.AssignedStaffId); // terminal task untouched

        var forwardAudit = await verifyDb.AuditLogs.SingleAsync(a => a.EntityType == "TaskItem" && a.EntityId == openTask.Id && a.Action == "forward");
        Assert.Equal("system", forwardAudit.Actor);
    }

    /// P9-12: LeaveFrom/LeaveTo required together when marking OnLeave.
    [Fact]
    public async Task UpdateStatus_ToOnLeave_RequiresBothLeaveDates()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var user = new User { Username = "leave-validation-9004", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Active };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var (client, _) = await _client.AsAdminAsync();

        var missingBoth = await client.PatchAsJsonAsync($"/api/users/{user.Id}/status", new { status = "OnLeave" });
        Assert.Equal(HttpStatusCode.BadRequest, missingBoth.StatusCode);

        var missingTo = await client.PatchAsJsonAsync($"/api/users/{user.Id}/status", new { status = "OnLeave", leaveFrom = DateTime.UtcNow });
        Assert.Equal(HttpStatusCode.BadRequest, missingTo.StatusCode);

        var fromAfterTo = await client.PatchAsJsonAsync($"/api/users/{user.Id}/status",
            new { status = "OnLeave", leaveFrom = DateTime.UtcNow.AddDays(5), leaveTo = DateTime.UtcNow });
        Assert.Equal(HttpStatusCode.BadRequest, fromAfterTo.StatusCode);

        var valid = await client.PatchAsJsonAsync($"/api/users/{user.Id}/status",
            new { status = "OnLeave", leaveFrom = DateTime.UtcNow, leaveTo = DateTime.UtcNow.AddDays(5) });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        // Fresh scope — the `db` above still has `user` tracked from its own Add/SaveChanges
        // call and would return that stale in-memory copy instead of re-querying.
        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var updated = await verifyDb.Users.SingleAsync(u => u.Id == user.Id);
        Assert.Equal(UserStatus.OnLeave, updated.Status);
        Assert.NotNull(updated.LeaveFrom);
        Assert.NotNull(updated.LeaveTo);
    }

    [Fact]
    public async Task UpdateStatus_AdminTarget_RejectsInactiveAndOnLeave()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var adminTarget = new User { Username = "admin-target-9006", PasswordHash = "unused", Role = UserRole.Admin, Status = UserStatus.Active };
        db.Users.Add(adminTarget);
        await db.SaveChangesAsync();

        var (client, _) = await _client.AsAdminAsync();

        var toInactive = await client.PatchAsJsonAsync($"/api/users/{adminTarget.Id}/status", new { status = "Inactive" });
        Assert.Equal(HttpStatusCode.BadRequest, toInactive.StatusCode);

        var toOnLeave = await client.PatchAsJsonAsync($"/api/users/{adminTarget.Id}/status",
            new { status = "OnLeave", leaveFrom = DateTime.UtcNow, leaveTo = DateTime.UtcNow.AddDays(5) });
        Assert.Equal(HttpStatusCode.BadRequest, toOnLeave.StatusCode);

        using var verifyScope = factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var unchanged = await verifyDb.Users.SingleAsync(u => u.Id == adminTarget.Id);
        Assert.Equal(UserStatus.Active, unchanged.Status);
    }

    [Fact]
    public async Task UpdateStatus_StaffTarget_StillAllowed()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();
        var staffTarget = new User { Username = "staff-target-9007", PasswordHash = "unused", Role = UserRole.Staff, Status = UserStatus.Active };
        db.Users.Add(staffTarget);
        await db.SaveChangesAsync();

        var (client, _) = await _client.AsAdminAsync();
        var response = await client.PatchAsJsonAsync($"/api/users/{staffTarget.Id}/status", new { status = "Inactive" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private class UserDtoShape
    {
        public long Id { get; set; }
        public string Username { get; set; } = default!;
        public string? FullName { get; set; }
        public string Role { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateTime? LeaveFrom { get; set; }
        public DateTime? LeaveTo { get; set; }
    }
}
