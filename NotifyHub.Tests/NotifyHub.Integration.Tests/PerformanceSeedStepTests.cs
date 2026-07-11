using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Infrastructure.Persistence;
using NotifyHub.Infrastructure.Seed;
using Xunit;

namespace NotifyHub.Integration.Tests;

/// FR-010: PerformanceSeedStep's own re-run idempotency and outbound/inbound split, using a
/// small injected target count (not the production 50,000) so this stays fast.
///
/// Deliberately does NOT use CustomWebApplicationFactory: Program.cs registers
/// PerformanceSeedStep as a real IDbSeedStep that runs automatically at Api startup (capped
/// to a tiny count there via Seed:PerformanceMessageCount so the rest of the InMemory suite
/// stays fast — see CustomWebApplicationFactory), which would trip this step's own
/// idempotency marker before these tests get to call RunAsync explicitly. A fresh, isolated
/// DbContext sidesteps that collision entirely.
public class PerformanceSeedStepTests
{
    [Fact]
    public async Task RunAsync_ReRunning_DoesNotDuplicate()
    {
        await using var db = CreateContext();
        await EnsureTemplateExistsAsync(db);

        var step = new PerformanceSeedStep(targetMessageCount: 200);
        await step.RunAsync(db, CancellationToken.None);
        var countAfterFirstRun = await db.OutboundMessages.CountAsync() + await db.InboundMessages.CountAsync();

        await step.RunAsync(db, CancellationToken.None);
        var countAfterSecondRun = await db.OutboundMessages.CountAsync() + await db.InboundMessages.CountAsync();

        Assert.Equal(countAfterFirstRun, countAfterSecondRun);
        Assert.True(countAfterFirstRun >= 200);
    }

    [Fact]
    public async Task RunAsync_OnlyCreatesTerminalStatusMessages_NoneQueued()
    {
        await using var db = CreateContext();
        await EnsureTemplateExistsAsync(db);

        var step = new PerformanceSeedStep(targetMessageCount: 200);
        await step.RunAsync(db, CancellationToken.None);

        var perfSeedMessages = await db.OutboundMessages
            .Where(m => m.TriggerReference != null && m.TriggerReference.StartsWith("perfseed:"))
            .ToListAsync();

        Assert.NotEmpty(perfSeedMessages);
        Assert.All(perfSeedMessages, m => Assert.NotEqual(MessageStatus.Queued, m.Status));
    }

    [Fact]
    public async Task RunAsync_DoesNothing_WhenNoTemplatesExist()
    {
        await using var db = CreateContext();

        var step = new PerformanceSeedStep(targetMessageCount: 200);
        await step.RunAsync(db, CancellationToken.None);

        Assert.False(await db.Patients.AnyAsync());
        Assert.False(await db.OutboundMessages.AnyAsync());
    }

    private static NotifyHubDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<NotifyHubDbContext>()
            .UseInMemoryDatabase($"perf-seed-test-{Guid.NewGuid()}")
            .Options;
        return new NotifyHubDbContext(options);
    }

    private static async Task EnsureTemplateExistsAsync(NotifyHubDbContext db)
    {
        db.MessageTemplates.Add(new MessageTemplate
        {
            Name = "Perf seed test template",
            Body = "Test body",
            TriggerType = TriggerType.AppointmentReminder,
            OffsetHours = 48,
        });
        await db.SaveChangesAsync();
    }
}
