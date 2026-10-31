using Fantasoccer.Application.Core.Notifications;

namespace Fantasoccer.Application.Api.Notifications;

public sealed class SqlPushWorker(IServiceScopeFactory scopes, ILogger<SqlPushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var attemptId = Guid.NewGuid();
            var stage = "market-producer";
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MarketPushProducer>().RunAsync(stoppingToken);
                stage = "matchday-producer";
                await scope.ServiceProvider.GetRequiredService<MatchdayPushProducer>().RunAsync(FantacalcioTime.ActualYear, stoppingToken);
                stage = "dispatch";
                await scope.ServiceProvider.GetRequiredService<PushDispatcher>().DispatchAsync(cancellationToken: stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("SQL push cycle failed at {Stage} ({FailureType}); attempt {AttemptId}. Leased deliveries will be retried; no exception message, URLs or payloads logged.", stage, error.GetType().Name, attemptId); }
        }
    }
}
