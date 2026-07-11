using NotifyHub.Infrastructure.Reminders;

namespace NotifyHub.Worker;

/// FR-009/§11/§14: appointment reminder scheduler, polled every 15 minutes (locked
/// decision — configurable via Reminders:PollIntervalSeconds for tests/tuning, same
/// pattern as EscalationWorker).
public class ReminderWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<ReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ErrorRetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromSeconds(configuration.GetValue("Reminders:PollIntervalSeconds", 900));

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var scheduler = scope.ServiceProvider.GetRequiredService<ReminderScheduler>();

            try
            {
                await scheduler.RunAsync(stoppingToken);
                await Task.Delay(pollInterval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Reminder scheduler: poll cycle failed, retrying in {Delay}", ErrorRetryDelay);
                await Task.Delay(ErrorRetryDelay, stoppingToken);
            }
        }
    }
}
