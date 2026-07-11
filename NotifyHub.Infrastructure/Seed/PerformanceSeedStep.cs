using Microsoft.EntityFrameworkCore;
using NotifyHub.Domain.Entities;
using NotifyHub.Domain.Enums;
using NotifyHub.Domain.Messaging;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// FR-010: seeds enough message volume to exercise the required indexes
/// (outbound_messages(status,next_retry_at)/(thread_id,created_at), inbound_messages
/// (thread_id,received_at), threads(assigned_staff_id)) and paginated queries at realistic
/// scale.
///
/// Spread thin across many new synthetic patients/threads (default: 1,000 threads, ~50
/// messages/thread) rather than piled onto the small existing demo patient set —
/// concentrating this volume on today's handful of demo patients would (a) not exercise
/// ThreadsController.List's pagination at all (still just a handful of rows) and (b) balloon
/// a single thread's message history to thousands of rows, which ThreadsController.Detail
/// loads entirely unpaginated (a separate, already-flagged risk — see STATUS.md's Final
/// review checklist). All seeded messages get terminal statuses (Delivered/Failed) so
/// DispatcherWorker's live poll never picks any of them up — this is historical volume for
/// query/pagination performance, not a live send queue.
///
/// Idempotent independently of DemoOutboundMessageSeedStep (which only checks "any outbound
/// message exists at all", and would otherwise interfere with this step's own "already
/// seeded" logic either way round): this step checks for its own patient-name marker prefix
/// instead of message count.
///
/// targetMessageCount is a constructor parameter (not a hardcoded 50,000) so tests can seed a
/// much smaller number and stay fast; production DI registers the default (50,000).
public class PerformanceSeedStep(int targetMessageCount = 50_000) : IDbSeedStep
{
    private const string PatientNamePrefix = "PerfSeed Patient ";
    private const int MessagesPerThreadTarget = 50;
    private const int MinThreadCount = 10;
    private const int MaxThreadCount = 1_000;
    private const int BatchSize = 2_000;
    private const double OutboundRatio = 0.9;

    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        if (await db.Patients.AnyAsync(p => p.Name.StartsWith(PatientNamePrefix), ct))
            return;

        var templates = await db.MessageTemplates.ToListAsync(ct);
        if (templates.Count == 0)
            return;

        // Thread count scales with the message target (~50 messages/thread) instead of a
        // fixed 1,000 regardless of scale — so a small test-injected targetMessageCount
        // doesn't still seed 1,000 patients/threads just to hold a handful of messages.
        var threadCount = Math.Clamp(targetMessageCount / MessagesPerThreadTarget, MinThreadCount, MaxThreadCount);

        var patients = Enumerable.Range(1, threadCount)
            .Select(i => new Patient { Name = $"{PatientNamePrefix}{i:D5}", Phone = $"+1777{i:D7}" })
            .ToList();
        db.Patients.AddRange(patients);
        await db.SaveChangesAsync(ct);

        var threads = patients.Select(p => new ConversationThread { PatientId = p.Id }).ToList();
        db.Threads.AddRange(threads);
        await db.SaveChangesAsync(ct);

        var now = DateTime.UtcNow;
        var random = new Random(12345);
        var outboundCount = (int)(targetMessageCount * OutboundRatio);
        var inboundCount = targetMessageCount - outboundCount;

        // Bulk insert of brand-new, unrelated entities — change-detection scanning brings no
        // benefit here and gets slower as the tracked set grows, so it's disabled for the
        // duration of both loops and restored afterward.
        db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            await SeedOutboundMessagesAsync(db, patients, threads, templates, outboundCount, now, random, ct);
            await SeedInboundMessagesAsync(db, threads, inboundCount, now, random, ct);
        }
        finally
        {
            db.ChangeTracker.AutoDetectChangesEnabled = true;
        }
    }

    private static async Task SeedOutboundMessagesAsync(
        NotifyHubDbContext db, List<Patient> patients, List<ConversationThread> threads,
        List<MessageTemplate> templates, int count, DateTime now, Random random, CancellationToken ct)
    {
        var batch = new List<OutboundMessage>(BatchSize);

        for (var i = 0; i < count; i++)
        {
            var patient = patients[i % patients.Count];
            var thread = threads[i % threads.Count];
            var template = templates[random.Next(templates.Count)];
            var triggerReference = $"perfseed:{i}";

            batch.Add(new OutboundMessage
            {
                PatientId = patient.Id,
                ThreadId = thread.Id,
                TemplateId = template.Id,
                SenderType = SenderType.System,
                TriggerReference = triggerReference,
                RenderedBody = template.Body,
                CreatedAt = now.AddMinutes(-random.Next(0, 60 * 24 * 90)),
                Status = random.NextDouble() < 0.9 ? MessageStatus.Delivered : MessageStatus.Failed,
                IdempotencyKey = IdempotencyKeyGenerator.Generate(patient.Id, template.Id, triggerReference),
                AttemptCount = 1,
            });

            if (batch.Count >= BatchSize)
                await FlushAsync(db, db.OutboundMessages, batch, ct);
        }

        await FlushAsync(db, db.OutboundMessages, batch, ct);
    }

    private static async Task SeedInboundMessagesAsync(
        NotifyHubDbContext db, List<ConversationThread> threads, int count, DateTime now, Random random, CancellationToken ct)
    {
        var batch = new List<InboundMessage>(BatchSize);

        for (var i = 0; i < count; i++)
        {
            var thread = threads[i % threads.Count];

            batch.Add(new InboundMessage
            {
                ThreadId = thread.Id,
                Body = "Perf-seed inbound message",
                ReceivedAt = now.AddMinutes(-random.Next(0, 60 * 24 * 90)),
            });

            if (batch.Count >= BatchSize)
                await FlushAsync(db, db.InboundMessages, batch, ct);
        }

        await FlushAsync(db, db.InboundMessages, batch, ct);
    }

    private static async Task FlushAsync<T>(NotifyHubDbContext db, DbSet<T> set, List<T> batch, CancellationToken ct) where T : class
    {
        if (batch.Count == 0)
            return;

        set.AddRange(batch);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
        batch.Clear();
    }
}
