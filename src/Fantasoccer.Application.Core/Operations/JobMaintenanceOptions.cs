namespace Fantasoccer.Application.Core.Operations;

public sealed class JobMaintenanceOptions
{
    public int HallBatchSize { get; set; } = 1000;
    public int RunRetentionDays { get; set; } = 30;
    public int MaxConcurrentJobs { get; set; } = 4;
}
