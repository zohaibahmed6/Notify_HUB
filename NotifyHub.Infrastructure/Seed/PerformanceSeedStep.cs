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
/// seeded" logic either way round): this step checks for its own patient-phone marker prefix
/// instead of message count.
///
/// targetMessageCount is a constructor parameter (not a hardcoded 50,000) so tests can seed a
/// much smaller number and stay fast; production DI registers the default (50,000).
public class PerformanceSeedStep(int targetMessageCount = 50_000) : IDbSeedStep
{
    // Idempotency + synthetic-patient marker: this phone prefix is unique to this step (the
    // small demo roster in PatientAppointmentSeedStep uses +15550100xxx), so it doubles as
    // the "already seeded" check without needing a placeholder name prefix.
    private const string PatientPhonePrefix = "+1777";
    private const int MessagesPerThreadTarget = 50;
    private const int MinThreadCount = 10;
    private const int MaxThreadCount = 1_000;
    private const int BatchSize = 2_000;
    private const double OutboundRatio = 0.9;

    // Realistic sample names (not real patient data — BR-006) instead of "PerfSeed Patient
    // 00001".. placeholders, so this volume-seed roster reads like real patients too. Four
    // locale pools (Pakistani English / Indian / Chinese / Japanese), first/last names never
    // mixed across locales so combinations stay authentic; GenerateName round-robins the
    // locale by index so the mix stays balanced across however many patients are seeded.
    private static readonly (string[] FirstNames, string[] LastNames)[] NamePools =
    [
        // Pakistani English
        (
            ["Ahmed", "Bilal", "Usman", "Hamza", "Ayesha", "Sana", "Zainab", "Imran", "Faisal", "Nadia", "Tariq", "Sara", "Kamran", "Mehwish", "Adeel", "Sobia", "Waqas", "Shazia", "Junaid", "Farah"],
            ["Khan", "Raza", "Malik", "Sheikh", "Farooq", "Qureshi", "Siddiqui", "Chaudhry", "Baig", "Mahmood", "Iqbal", "Yousuf", "Aslam", "Rashid", "Naveed", "Hussain", "Akhtar", "Zafar", "Anjum", "Hassan"]
        ),
        // Indian
        (
            ["Rohan", "Priya", "Arjun", "Ananya", "Vikram", "Neha", "Rahul", "Kavya", "Aditya", "Sneha", "Karan", "Divya", "Sanjay", "Pooja", "Manoj", "Ritu", "Arvind", "Meera", "Suresh", "Lakshmi"],
            ["Sharma", "Patel", "Mehta", "Iyer", "Nair", "Gupta", "Verma", "Reddy", "Rao", "Joshi", "Malhotra", "Menon", "Kapoor", "Desai", "Pillai", "Bhatt", "Chawla", "Krishnan", "Iyengar", "Subramaniam"]
        ),
        // Chinese
        (
            ["Wei", "Li", "Jing", "Hui", "Yan", "Feng", "Xin", "Mei", "Jun", "Ling", "Tao", "Fang", "Chao", "Hong", "Bo", "Xia", "Ping", "Rui", "Yun", "Qiang"],
            ["Zhang", "Wang", "Chen", "Liu", "Yang", "Huang", "Zhao", "Wu", "Zhou", "Xu", "Sun", "Ma", "Gao", "Lin", "Zheng", "Liang", "Song", "Xie", "Han", "Deng"]
        ),
        // Japanese
        (
            ["Haruto", "Yui", "Sora", "Aoi", "Ren", "Hina", "Riku", "Sakura", "Sota", "Yuna", "Hayato", "Mio", "Kaito", "Rin", "Yuto", "Kokoro", "Daiki", "Nanami", "Ryusei", "Akari"],
            ["Sato", "Suzuki", "Takahashi", "Tanaka", "Watanabe", "Ito", "Yamamoto", "Nakamura", "Kobayashi", "Kato", "Yoshida", "Yamada", "Sasaki", "Matsumoto", "Inoue", "Kimura", "Shimizu", "Hayashi", "Saito", "Mori"]
        ),
    ];

    private static string GenerateName(int index)
    {
        var pool = NamePools[index % NamePools.Length];
        var comboIndex = index / NamePools.Length;
        var firstName = pool.FirstNames[comboIndex % pool.FirstNames.Length];
        var lastName = pool.LastNames[(comboIndex / pool.FirstNames.Length) % pool.LastNames.Length];
        return $"{firstName} {lastName}";
    }

    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct)
    {
        if (await db.Patients.AnyAsync(p => p.Phone.StartsWith(PatientPhonePrefix), ct))
            return;

        var templates = await db.MessageTemplates.ToListAsync(ct);
        if (templates.Count == 0)
            return;

        // Thread count scales with the message target (~50 messages/thread) instead of a
        // fixed 1,000 regardless of scale — so a small test-injected targetMessageCount
        // doesn't still seed 1,000 patients/threads just to hold a handful of messages.
        var threadCount = Math.Clamp(targetMessageCount / MessagesPerThreadTarget, MinThreadCount, MaxThreadCount);

        var patients = Enumerable.Range(1, threadCount)
            .Select(i => new Patient { Name = GenerateName(i - 1), Phone = $"{PatientPhonePrefix}{i:D7}" })
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
                PduCount = PduSegmentCalculator.CalculateSegmentCount(template.Body),
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
