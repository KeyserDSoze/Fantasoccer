namespace Fantasoccer.Business.Services;

public interface IPlayerImageRefresher
{
    Task<bool> ReloadAsync(RealPlayer player, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlayerImageReloadResult>> ReloadManyAsync(IReadOnlyCollection<RealPlayer> players, CancellationToken cancellationToken);
    Task DownloadMissingAsync(CancellationToken cancellationToken);
}
public sealed record PlayerImageReloadResult(string PlayerKey, string PlayerName, bool Succeeded, string? Error);
