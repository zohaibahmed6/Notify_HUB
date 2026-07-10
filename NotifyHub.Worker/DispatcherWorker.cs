using NotifyHub.Infrastructure.Messaging;

namespace NotifyHub.Worker;

/// FR-001/FR-003: continuously polls for queued/due-for-retry outbound messages and
/// dispatches them. The claim/render/dispatch logic lives in MessageDispatcher
/// (Infrastructure) so it's testable independently of this hosting loop.
public class DispatcherWorker(IServiceScopeFactory scopeFactory, ILogger<DispatcherWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ErrorRetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<MessageDispatcher>();

            try
            {
                var processed = await dispatcher.DispatchDueMessagesAsync(stoppingToken);
                if (processed > 0)
                    logger.LogInformation("Dispatcher: processed {Count} due message(s)", processed);

                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Dispatcher: poll cycle failed, retrying in {Delay}", ErrorRetryDelay);
                await Task.Delay(ErrorRetryDelay, stoppingToken);
            }
        }
    }
}
