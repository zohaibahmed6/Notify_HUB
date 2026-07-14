using NotifyHub.Infrastructure.Escalation;

namespace NotifyHub.Worker;

/// FR-008/BR-004/§11: periodic escalation job. Interval is an inference (§11 says only
/// "Periodic", no number given) — 1 minute keeps the demo responsive without hammering
/// the DB; configurable via Escalation:PollIntervalSeconds.
public class EscalationWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<EscalationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ErrorRetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(configuration.GetValue("Escalation:PollIntervalSeconds", 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var job = scope.ServiceProvider.GetRequiredService<EscalationJob>();

            try
            {
                await job.EscalateOverdueTasksAsync(stoppingToken);
                await job.RevertExpiredLeaveAsync(stoppingToken); // P9-12: piggybacks on this poll loop
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Escalation job: poll cycle failed, retrying in {Delay}", ErrorRetryDelay);
                await Task.Delay(ErrorRetryDelay, stoppingToken);
            }
        }
    }
}
