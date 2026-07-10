using Microsoft.EntityFrameworkCore;
using NotifyHub.Infrastructure.Persistence;

namespace NotifyHub.Worker;

/// Step-1 placeholder: proves the Worker container can reach the database independently
/// of the Api container's migration/seed step. The real dispatcher, reminder scheduler,
/// and escalation job land in step 2+.
///
/// The Worker only depends on mysql being healthy (§11), not on Api having finished
/// migrating — no Api healthcheck endpoint exists to gate on. So this loop retries with
/// a fixed backoff rather than assuming the schema exists on first attempt.
public class PlaceholderHeartbeatWorker(IServiceScopeFactory scopeFactory, ILogger<PlaceholderHeartbeatWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<NotifyHubDbContext>();

            try
            {
                var canConnect = await db.Database.CanConnectAsync(stoppingToken);
                logger.LogInformation("Worker heartbeat: database reachable = {CanConnect}", canConnect);
                await Task.Delay(HeartbeatInterval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Worker heartbeat: database not reachable yet, retrying in {Delay}", RetryDelay);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }
}
