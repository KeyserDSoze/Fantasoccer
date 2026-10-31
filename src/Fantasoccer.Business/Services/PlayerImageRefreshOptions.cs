namespace Fantasoccer.Business.Services;

public sealed class PlayerImageRefreshOptions
{
    public TimeSpan BatchTimeout { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan ImageTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public int ParallelDownloads { get; set; } = 4;
}
