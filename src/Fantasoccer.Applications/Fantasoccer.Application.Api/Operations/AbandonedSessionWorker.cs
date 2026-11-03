using Fantasoccer.Infrastructure.PostgreSql;

namespace Fantasoccer.Application.Api.Operations;

// After a deploy the previous process may have been cut off holding the global write lock (see AbandonedSessionReaper).
// For the first minutes of a new process, look for what it left behind and release it.
public sealed class AbandonedSessionWorker(IServiceScopeFactory scopes, ILogger<AbandonedSessionWorker> logger) : BackgroundService
{
    private static readonly TimeSpan TransactionAge = TimeSpan.FromSeconds(60);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));
        for (var round = 0; round < 45 && await WaitAsync(timer, stoppingToken); round++)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var ended = await AbandonedSessionReaper.TerminateAsync(scope.ServiceProvider.GetRequiredService<FantasoccerDbContext>(),
                    AbandonedSessionReaper.OwnApplicationName, TransactionAge, stoppingToken);
                if (ended > 0) logger.LogWarning("Released {Count} database session(s) left in a transaction for over {Seconds} s by a previous API process (deploy or restart).", ended, TransactionAge.TotalSeconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception error) { logger.LogWarning("Looking for abandoned database sessions failed ({FailureType}).", error.GetType().Name); }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try { return await timer.WaitForNextTickAsync(stoppingToken); }
        catch (OperationCanceledException) { return false; }
    }
}
