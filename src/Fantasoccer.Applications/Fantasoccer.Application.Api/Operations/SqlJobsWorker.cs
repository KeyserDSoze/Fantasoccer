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
                var attemptId = Guid.NewGuid();
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var result = await scope.ServiceProvider.GetRequiredService<SqlJobApplicationService>().RunScheduledAsync(name, FantacalcioTime.ActualYear, stoppingToken);
                    if (result != null) logger.LogInformation("SQL job {JobName} finished with {Status}, code {Code}; run {RunId}", name, result.Status, result.Code, result.RunId);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception error) { logger.LogWarning("SQL job {JobName} did not finish ({FailureType}); attempt {AttemptId}. Its lease will expire safely.", name, error.GetType().Name, attemptId); }
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
