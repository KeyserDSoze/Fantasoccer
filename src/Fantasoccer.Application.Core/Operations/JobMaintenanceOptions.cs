namespace Fantasoccer.Application.Core.Operations;

public sealed class JobMaintenanceOptions
{
    public int HallBatchSize { get; set; } = 1000;
    public int RunRetentionDays { get; set; } = 30;
    public int MaxConcurrentJobs { get; set; } = 4;
    // After a deploy App Service stops the previous container about half a minute after this one starts, and for a few minutes the
    // database connections opened around that moment can hang until their 120 s timeout. Background work waits this long first.
    public int StartDelaySeconds { get; set; } = 300;
}
