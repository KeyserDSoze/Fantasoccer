using Fantasoccer.Application.Core.Notifications;

namespace Fantasoccer.Application.Api.Notifications;

public sealed class SqlPushWorker(IServiceScopeFactory scopes, ILogger<SqlPushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MarketPushProducer>().RunAsync(stoppingToken);
                await scope.ServiceProvider.GetRequiredService<MatchdayPushProducer>().RunAsync(FantacalcioTime.ActualYear, stoppingToken);
                await scope.ServiceProvider.GetRequiredService<PushDispatcher>().DispatchAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch { logger.LogWarning("SQL push dispatch did not finish. Leased deliveries will be retried; provider URLs and payloads are not logged."); }
        }
    }
}
