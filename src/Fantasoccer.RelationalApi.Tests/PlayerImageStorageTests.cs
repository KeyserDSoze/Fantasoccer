using Azure.Storage.Blobs;
using Fantasoccer.Application.Api.Storage;
using Fantasoccer.Business.Services;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class PlayerImageStorageTests
{
    [Fact]
    public async Task Direct_blob_storage_keeps_player_paths_content_headers_and_allows_photo_replacement()
    {
        var connection = Environment.GetEnvironmentVariable("FANTASOCCER_TEST_STORAGE");
        if (string.IsNullOrWhiteSpace(connection)) Assert.Skip("Set FANTASOCCER_TEST_STORAGE to run Azurite image storage tests.");
        if (!connection.Contains("AccountName=migrationtest;", StringComparison.Ordinal) || !connection.Contains("127.0.0.1", StringComparison.Ordinal) && !connection.Contains("azurite", StringComparison.Ordinal))
            throw new InvalidOperationException("Only the dedicated local emulator is allowed.");
        var container = new BlobContainerClient(connection, "images-test-" + Guid.NewGuid().ToString("N"));
        await container.CreateIfNotExistsAsync();
        try
        {
            IPlayerImageStore store = new BlobPlayerImageStorage(container);
            const string name = "players/mariorossi.jpg";
            Assert.False(await store.ExistsAsync(name, CancellationToken.None));
            await store.UploadAsync(name, [1, 2, 3], "image/jpeg", "public, max-age=300", CancellationToken.None);
            Assert.True(await store.ExistsAsync(name, CancellationToken.None));
            var blob = container.GetBlobClient(name);
            var before = (await blob.GetPropertiesAsync()).Value;
            Assert.Equal("image/jpeg", before.ContentType);
            Assert.Equal("public, max-age=300", before.CacheControl);
            await store.UploadAsync(name, [4, 5], "image/png", "public, max-age=300", CancellationToken.None);
            var after = (await blob.DownloadContentAsync()).Value;
            Assert.NotEqual(before.ETag, after.Details.ETag);
            Assert.Equal("image/png", after.Details.ContentType);
            Assert.Equal(new byte[] { 4, 5 }, after.Content.ToArray());
        }
        finally { await container.DeleteIfExistsAsync(); }
    }
}
