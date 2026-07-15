using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Tasks;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// Seeds bulk demo task data so the Tasks screen isn't just a handful of rows — mirrors why
/// PerformanceSeedStep exists for Threads/Messages. Registered after PerformanceSeedStep so it
/// can reuse the ~1,000 synthetic patients/threads that step already creates (plus the 10
/// PatientAppointmentSeedStep demo patients) instead of duplicating patient-generation logic;
/// only pads with its own placeholder patients/threads when fewer than targetTaskCount already
/// exist (e.g. test factories, which cap PerformanceSeedStep down for speed).
///
/// targetTaskCount is a constructor parameter (not a hardcoded 1,000) so tests can seed a much
/// smaller number and stay fast; production DI registers the default (1,000), same convention
/// as PerformanceSeedStep's targetMessageCount.
public class TaskSeedStep(int targetTaskCount = 1_000) : IDbSeedStep
{
    // Distinct from PerformanceSeedStep's "+1777" marker so the two steps' idempotency/fallback
    // patients never collide.
    private const string FallbackPatientPhonePrefix = "+1778";

    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        if (await db.Tasks.AnyAsync(ct))
            return;

        var activeUsers = await db.Users
            .Where(u => u.Status == UserStatus.Active)
            .OrderBy(u => u.Id)
            .ToListAsync(ct);
        if (activeUsers.Count == 0)
            return;

        var threads = await db.Threads.OrderBy(t => t.Id).Take(targetTaskCount).ToListAsync(ct);

        if (threads.Count < targetTaskCount)
            threads.AddRange(await CreateFallbackThreadsAsync(db, targetTaskCount - threads.Count, ct));

        var now = DateTime.UtcNow;
        var random = new Random(12345);

        var tasks = new List<TaskItem>(targetTaskCount);
        for (var i = 0; i < targetTaskCount; i++)
        {
            var thread = threads[i];
            var user = activeUsers[i % activeUsers.Count];
            var priority = (TaskPriority)(i % 4);
            var taskType = (TaskType)(i % 9);
            var status = StatusForIndex(i % 100);

            tasks.Add(new TaskItem
            {
                ThreadId = thread.Id,
                Priority = priority,
                Status = status,
                DueAt = DueAtForStatus(status, priority, now, random),
                AssignedStaffId = user.Id,
                OriginalOwnerId = user.Id,
                TaskType = taskType,
                IsActive = true,
            });
        }

        db.Tasks.AddRange(tasks);
        await db.SaveChangesAsync(ct);
    }

    private static NotifyHubTaskStatus StatusForIndex(int bucket) => bucket switch
    {
        < 20 => NotifyHubTaskStatus.Open,
        < 45 => NotifyHubTaskStatus.InProgress,
        < 75 => NotifyHubTaskStatus.Completed,
        < 85 => NotifyHubTaskStatus.Escalated,
        _ => NotifyHubTaskStatus.Cancelled,
    };

    // EscalationJob only ever touches overdue Open/InProgress tasks (Completed/Cancelled/
    // Escalated are excluded from its query), so those two statuses must stay future-dated —
    // otherwise the next escalation poll would flip freshly-seeded "in progress" tasks to
    // Escalated and reassign them, corrupting the seeded mix. The other three are already
    // resolved/overdue by definition, so a randomized past date reads more realistically than a
    // future one.
    private static DateTime DueAtForStatus(NotifyHubTaskStatus status, TaskPriority priority, DateTime now, Random random) => status switch
    {
        NotifyHubTaskStatus.Open or NotifyHubTaskStatus.InProgress => TaskDueDateDefaults.DefaultDueAt(priority, now),
        NotifyHubTaskStatus.Escalated => now.AddDays(-random.Next(1, 14)),
        _ => now.AddDays(-random.Next(1, 60)),
    };

    private static async Task<List<ConversationThread>> CreateFallbackThreadsAsync(NotifyHubDbContext db, int count, CancellationToken ct)
    {
        var patients = Enumerable.Range(1, count)
            .Select(i => new Patient { Name = $"TaskSeed Patient {i:D4}", Phone = $"{FallbackPatientPhonePrefix}{i:D7}" })
            .ToList();
        db.Patients.AddRange(patients);
        await db.SaveChangesAsync(ct);

        var threads = patients.Select(p => new ConversationThread { PatientId = p.Id }).ToList();
        db.Threads.AddRange(threads);
        await db.SaveChangesAsync(ct);

        return threads;
    }
}
