namespace Fantasoccer.Application.Api.Mcp.Storage;

public sealed class OAuthRetentionService(IOAuthDocumentStorage storage, ILogger<OAuthRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(6));
        do
        {
            try
            {
                var count = await storage.PruneAsync(DateTimeOffset.UtcNow, stoppingToken);
                logger.LogInformation("OAuth retention removed {Count} expired documents.", count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception exception) { logger.LogError("OAuth retention failed ({ExceptionType}).", exception.GetType().Name); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
