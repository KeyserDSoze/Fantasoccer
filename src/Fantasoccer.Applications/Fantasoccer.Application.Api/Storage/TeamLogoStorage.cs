using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Fantasoccer.Application.Core.Teams;

namespace Fantasoccer.Application.Api.Storage;

public sealed class BlobTeamLogoStorage(IConfiguration configuration) : ITeamLogoStorage
{
    private readonly BlobContainerClient _logos = new(configuration.GetConnectionString("Storage"), "logos");
    public async Task<byte[]?> DownloadAsync(string name, CancellationToken cancellationToken)
    {
        try { return (await _logos.GetBlobClient(name).DownloadContentAsync(cancellationToken)).Value.Content.ToArray(); }
        catch (RequestFailedException error) when (error.Status == 404) { return null; }
    }
    public async Task UploadAsync(string name, byte[] image, string contentType, CancellationToken cancellationToken) =>
        await _logos.GetBlobClient(name).UploadAsync(BinaryData.FromBytes(image), new BlobUploadOptions
        {
            Conditions = new() { IfNoneMatch = ETag.All }, HttpHeaders = new() { ContentType = contentType, CacheControl = "public, max-age=31536000, immutable" }
        }, cancellationToken);
}

public sealed class InMemoryTeamLogoStorage : ITeamLogoStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _images = new();
    public Task<byte[]?> DownloadAsync(string name, CancellationToken cancellationToken) => Task.FromResult(_images.TryGetValue(name, out var image) ? image.ToArray() : null);
    public Task UploadAsync(string name, byte[] image, string contentType, CancellationToken cancellationToken)
    { if (!_images.TryAdd(name, image.ToArray())) throw new InvalidOperationException("Logo version already exists."); return Task.CompletedTask; }
}
