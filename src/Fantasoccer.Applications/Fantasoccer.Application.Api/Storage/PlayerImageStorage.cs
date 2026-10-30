using System.Collections.Concurrent;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Fantasoccer.Business.Services;

namespace Fantasoccer.Application.Api.Storage;

public sealed class BlobPlayerImageStorage(BlobContainerClient images) : IPlayerImageStore
{
    public BlobPlayerImageStorage(IConfiguration configuration) : this(new BlobContainerClient(configuration.GetConnectionString("Storage"), "images")) { }
    public async Task<bool> ExistsAsync(string name, CancellationToken cancellationToken) =>
        (await images.GetBlobClient(name).ExistsAsync(cancellationToken)).Value;
    public async Task UploadAsync(string name, byte[] image, string contentType, string cacheControl, CancellationToken cancellationToken) =>
        await images.GetBlobClient(name).UploadAsync(BinaryData.FromBytes(image), new BlobUploadOptions
        { HttpHeaders = new() { ContentType = contentType, CacheControl = cacheControl } }, cancellationToken);
}

public sealed class InMemoryPlayerImageStorage : IPlayerImageStore
{
    private readonly ConcurrentDictionary<string, byte[]> _images = new();
    public Task<bool> ExistsAsync(string name, CancellationToken cancellationToken) => Task.FromResult(_images.ContainsKey(name));
    public Task UploadAsync(string name, byte[] image, string contentType, string cacheControl, CancellationToken cancellationToken)
    { _images[name] = image.ToArray(); return Task.CompletedTask; }
}
