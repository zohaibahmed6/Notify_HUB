using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// A single, independently-idempotent seeding step. Each implementation checks
/// its own "already seeded" condition before writing, so restarting the stack
/// never re-seeds or duplicates data (§11).
public interface IDbSeedStep
{
    Task RunAsync(NotifyHubDbContext db, CancellationToken ct);
}
