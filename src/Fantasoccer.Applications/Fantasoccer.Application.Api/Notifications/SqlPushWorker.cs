using Fantasoccer.Application.Core.Notifications;

namespace Fantasoccer.Application.Api.Notifications;

public sealed class SqlPushWorker(IServiceScopeFactory scopes, ILogger<SqlPushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // A failed game producer must not prevent operational alerts or the
            // dispatch of deliveries already in the outbox. Each phase gets a
            // clean scope so a failed transaction cannot poison later phases.
            await PhaseAsync("market", service => service.GetRequiredService<MarketPushProducer>().RunAsync(stoppingToken), stoppingToken);
            await PhaseAsync("matchday", service => service.GetRequiredService<MatchdayPushProducer>().RunAsync(FantacalcioTime.ActualYear, stoppingToken), stoppingToken);
            await PhaseAsync("operational", service => service.GetRequiredService<OperationalAlertProducer>().RunAsync(FantacalcioTime.ActualYear, stoppingToken), stoppingToken);
            await PhaseAsync("dispatch", service => service.GetRequiredService<PushDispatcher>().DispatchAsync(cancellationToken: stoppingToken), stoppingToken);
        }
    }

    private async Task PhaseAsync(string stage, Func<IServiceProvider, Task> action, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) return;
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await action(scope.ServiceProvider);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error)
        {
            logger.LogWarning("SQL push phase {Stage} failed ({FailureType}); attempt {AttemptId}. No provider payload logged.", stage, error.GetType().Name, Guid.NewGuid());
            if (stage != "operational")
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<OperationalFailureRecorder>().RecordAsync("push_" + stage, "push_phase_failed");
            }
        }
    }
}
