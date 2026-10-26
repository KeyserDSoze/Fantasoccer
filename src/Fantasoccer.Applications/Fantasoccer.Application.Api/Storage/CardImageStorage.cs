using System.Collections.Concurrent;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Fantasoccer.Application.Core.Cards;

namespace Fantasoccer.Application.Api.Storage;

public sealed class BlobCardImageStorage(IConfiguration configuration) : ICardImageStorage
{
    private readonly BlobContainerClient _images = new(configuration.GetConnectionString("Storage"), "cardimages");
    public async Task UploadAsync(string name, byte[] image, string contentType, CancellationToken cancellationToken) =>
        await _images.GetBlobClient(name).UploadAsync(BinaryData.FromBytes(image), new BlobUploadOptions
        { Conditions = new() { IfNoneMatch = ETag.All }, HttpHeaders = new() { ContentType = contentType, CacheControl = "public, max-age=31536000, immutable" } }, cancellationToken);
}
public sealed class InMemoryCardImageStorage : ICardImageStorage
{
    private readonly ConcurrentDictionary<string, byte[]> _images = new();
    public Task UploadAsync(string name, byte[] image, string contentType, CancellationToken cancellationToken)
    { if (!_images.TryAdd(name, image.ToArray())) throw new InvalidOperationException("Image version already exists."); return Task.CompletedTask; }
}
