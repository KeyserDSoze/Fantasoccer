namespace Fantasoccer.Business.Services;

// The public container of the official player photos: one blob per player, replaced when a photo is reloaded.
public interface IPlayerImageStore
{
    Task<bool> ExistsAsync(string name, CancellationToken cancellationToken);
    Task UploadAsync(string name, byte[] image, string contentType, string cacheControl, CancellationToken cancellationToken);
}
