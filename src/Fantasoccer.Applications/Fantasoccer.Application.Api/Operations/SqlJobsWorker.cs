using Fantasoccer.Application.Core.Operations;

namespace Fantasoccer.Application.Api.Operations;

public sealed class SqlJobsWorker(IServiceScopeFactory scopes, ILogger<SqlJobsWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        do
        {
            foreach (var name in SqlJobSchedule.Names)
            {
                if (stoppingToken.IsCancellationRequested) return;
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<SqlJobApplicationService>().RunScheduledAsync(name, FantacalcioTime.ActualYear, stoppingToken);
                    if (result != null) logger.LogInformation("SQL job {JobName} finished with {Status}; run {RunId}", name, result.Status, result.RunId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch { logger.LogWarning("SQL job {JobName} did not finish; its lease will expire safely.", name); }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
