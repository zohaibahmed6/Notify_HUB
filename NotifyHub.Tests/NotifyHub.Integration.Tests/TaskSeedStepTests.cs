using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Seed;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// TaskSeedStep's own re-run idempotency and status/assignee distribution, using a small
/// injected target count (not the production 1,000) so this stays fast.
///
/// Deliberately does NOT use CustomWebApplicationFactory: Program.cs registers TaskSeedStep as
/// a real IDbSeedStep that runs automatically at Api startup (capped to a tiny count there via
/// Seed:TaskCount — see CustomWebApplicationFactory), which would trip this step's own
/// idempotency marker before these tests get to call RunAsync explicitly. A fresh, isolated
/// DbContext sidesteps that collision entirely, same pattern as PerformanceSeedStepTests.
public class TaskSeedStepTests
{
    [Fact]
    public async Task RunAsync_ReRunning_DoesNotDuplicate()
    {
        await using var db = CreateContext();
        await SeedUsersAndThreadsAsync(db, threadCount: 50);

        var step = new TaskSeedStep(targetTaskCount: 30);
        await step.RunAsync(db, CancellationToken.None);
        var countAfterFirstRun = await db.Tasks.CountAsync();

        await step.RunAsync(db, CancellationToken.None);
        var countAfterSecondRun = await db.Tasks.CountAsync();

        Assert.Equal(30, countAfterFirstRun);
        Assert.Equal(countAfterFirstRun, countAfterSecondRun);
    }

    [Fact]
    public async Task RunAsync_CoversAllStatuses_AndBothRoles()
    {
        await using var db = CreateContext();
        var (admin, staff) = await SeedUsersAndThreadsAsync(db, threadCount: 200);

        var step = new TaskSeedStep(targetTaskCount: 100);
        await step.RunAsync(db, CancellationToken.None);

        var tasks = await db.Tasks.ToListAsync();

        Assert.Equal(100, tasks.Count);
        Assert.All(Enum.GetValues<NotifyHubTaskStatus>(),
            status => Assert.Contains(tasks, t => t.Status == status));
        Assert.Contains(tasks, t => t.AssignedStaffId == admin.Id);
        Assert.Contains(tasks, t => t.AssignedStaffId == staff.Id);
    }

    [Fact]
    public async Task RunAsync_NeverAssignsInactiveUsers()
    {
        await using var db = CreateContext();
        var (_, _) = await SeedUsersAndThreadsAsync(db, threadCount: 50);
        db.Users.Add(new User
        {
            Username = "inactive-staff",
            PasswordHash = "hash",
            Role = UserRole.Staff,
            Status = UserStatus.Inactive,
        });
        await db.SaveChangesAsync();

        var step = new TaskSeedStep(targetTaskCount: 30);
        await step.RunAsync(db, CancellationToken.None);

        var assignedUserIds = await db.Tasks.Select(t => t.AssignedStaffId).Distinct().ToListAsync();
        var inactiveUserId = await db.Users.Where(u => u.Username == "inactive-staff").Select(u => u.Id).SingleAsync();

        Assert.DoesNotContain(inactiveUserId, assignedUserIds);
    }

    [Fact]
    public async Task RunAsync_OpenAndInProgressTasks_AreNeverOverdue()
    {
        await using var db = CreateContext();
        await SeedUsersAndThreadsAsync(db, threadCount: 200);

        var step = new TaskSeedStep(targetTaskCount: 100);
        await step.RunAsync(db, CancellationToken.None);

        var now = DateTime.UtcNow;
        var openOrInProgress = await db.Tasks
            .Where(t => t.Status == NotifyHubTaskStatus.Open || t.Status == NotifyHubTaskStatus.InProgress)
            .ToListAsync();

        Assert.NotEmpty(openOrInProgress);
        Assert.All(openOrInProgress, t => Assert.True(t.DueAt > now));
    }

    [Fact]
    public async Task RunAsync_PadsWithFallbackPatients_WhenNotEnoughThreadsExist()
    {
        await using var db = CreateContext();
        await SeedUsersAndThreadsAsync(db, threadCount: 5);

        var step = new TaskSeedStep(targetTaskCount: 30);
        await step.RunAsync(db, CancellationToken.None);

        Assert.Equal(30, await db.Tasks.CountAsync());
        Assert.Equal(30, await db.Threads.CountAsync());
        Assert.True(await db.Patients.AnyAsync(p => p.Phone.StartsWith("+1778")));
    }

    private static NotifyHubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotifyHubDbContext>()
            .UseInMemoryDatabase($"task-seed-test-{Guid.NewGuid()}")
            .Options;
        return new NotifyHubDbContext(options);
    }

    private static async Task<(User Admin, User Staff)> SeedUsersAndThreadsAsync(NotifyHubDbContext db, int threadCount)
    {
        var admin = new User { Username = "seed-admin", PasswordHash = "hash", Role = UserRole.Admin, Status = UserStatus.Active };
        var staff = new User { Username = "seed-staff", PasswordHash = "hash", Role = UserRole.Staff, Status = UserStatus.Active };
        db.Users.AddRange(admin, staff);
        await db.SaveChangesAsync();

        var patients = Enumerable.Range(1, threadCount)
            .Select(i => new Patient { Name = $"Test Patient {i}", Phone = $"+1999{i:D7}" })
            .ToList();
        db.Patients.AddRange(patients);
        await db.SaveChangesAsync();

        db.Threads.AddRange(patients.Select(p => new ConversationThread { PatientId = p.Id }));
        await db.SaveChangesAsync();

        return (admin, staff);
    }
}
