using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Fantasoccer.Infrastructure;

// Containers of the blob file storage that are not the distributed cache.
public static class FileStorage
{
    // Player photos, team logos and card images are served straight to browsers: their blobs (never the container
    // listing) are publicly readable.
    public static async Task EnsurePublicContainersAsync(string connectionString)
    {
        foreach (var name in new[] { "images", "logos", "cardimages" })
            await new BlobContainerClient(connectionString, name).CreateIfNotExistsAsync(PublicAccessType.Blob);
    }

    // The Data Protection key ring protects the application's tokens: it must never be readable anonymously.
    public static async Task ValidatePrivateKeyRingAsync(string connectionString)
    {
        var container = new BlobContainerClient(connectionString, "mcp-keys");
        await container.CreateIfNotExistsAsync();
        if ((await container.GetPropertiesAsync()).Value.PublicAccess != PublicAccessType.None)
            throw new InvalidOperationException("The Data Protection key ring container must be private.");
    }
}
