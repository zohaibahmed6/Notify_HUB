using Microsoft.Extensions.Logging;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Infrastructure.Seed;

/// Orchestrates all registered IDbSeedStep implementations, in DI-registration order.
/// Later steps (patients, appointments, 50k messages) register additional
/// IDbSeedStep implementations without any change to this class or to startup wiring.
public class DbSeedRunner(IEnumerable<IDbSeedStep> steps, ILogger<DbSeedRunner> logger)
{
    public async Task RunAsync(NotifyHubDbContext db, CancellationToken ct = default)
    {
        foreach (var step in steps)
        {
            logger.LogInformation("Running seed step {Step}", step.GetType().Name);
            await step.RunAsync(db, ct);
        }
    }
}
