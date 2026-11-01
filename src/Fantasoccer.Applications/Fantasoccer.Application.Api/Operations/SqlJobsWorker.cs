using Fantasoccer.Application.Core.Operations;
using Microsoft.Extensions.Options;

namespace Fantasoccer.Application.Api.Operations;

public sealed class SqlJobsWorker(IServiceScopeFactory scopes, ILogger<SqlJobsWorker> logger, IOptions<JobMaintenanceOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A slow provider in one job must not hold up every other job's timer.
        // The SQL lease still arbitrates this name/season across all replicas.
        // Bound active work to leave connections and CPU for HTTP and push.
        using var slots = new SemaphoreSlim(options.Value.MaxConcurrentJobs);
        await Task.WhenAll(SqlJobSchedule.Names.Select(name => RunLoopAsync(name, slots, stoppingToken)));
    }

    private async Task RunLoopAsync(string name, SemaphoreSlim slots, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            if (stoppingToken.IsCancellationRequested) return;
            try
            {
                await slots.WaitAsync(stoppingToken);
                var attemptId = Guid.NewGuid();
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<SqlJobApplicationService>().RunScheduledAsync(name, FantacalcioTime.ActualYear, stoppingToken);
                    if (result != null) logger.LogInformation("SQL job {JobName} finished with {Status}, code {Code}; run {RunId}", name, result.Status, result.Code, result.RunId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error) { logger.LogWarning("SQL job {JobName} did not finish ({FailureType}); attempt {AttemptId}. Its lease will expire safely.", name, error.GetType().Name, attemptId); }
                finally { slots.Release(); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
