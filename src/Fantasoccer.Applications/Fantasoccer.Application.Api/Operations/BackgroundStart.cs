namespace Fantasoccer.Application.Api.Operations;

public static class BackgroundStart
{
    // false when the application stops before the delay ends.
    public static async Task<bool> WaitAsync(int seconds, ILogger logger, string worker, CancellationToken stoppingToken)
    {
        if (seconds <= 0) return true;
        logger.LogInformation("Background {Worker} start in {Seconds} s (after the deploy swap).", worker, seconds);
        try { await Task.Delay(TimeSpan.FromSeconds(seconds), stoppingToken); return true; }
        catch (OperationCanceledException) { return false; }
    }
}
