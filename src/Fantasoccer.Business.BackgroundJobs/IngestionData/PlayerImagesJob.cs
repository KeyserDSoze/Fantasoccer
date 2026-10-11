using System.Text.Json;
using System.Timers;
using Fantasoccer.Business.Services;

namespace Fantasoccer.Engine.IngestionData;

public class PlayerImagesJob(PlayerImageService images, IFantasoccerLogger logger) : IBackgroundJob
{
    public async Task ActionToDoAsync(CancellationToken cancellationToken)
    {
        try
        {
            await images.DownloadMissingAsync(cancellationToken);
        }
        catch (HttpRequestException)
        {
            // An unavailable official catalog must not interrupt the scheduled job.
        }
        catch (JsonException)
        {
        }
    }

    public Task OnException(Exception exception) => logger.LogErrorAsync<PlayerImagesJob>(exception);
}
