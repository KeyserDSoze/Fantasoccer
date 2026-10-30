using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Api;

public sealed class McpBlobFactAttribute : FactAttribute
{
    public McpBlobFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FANTASOCCER_TEST_STORAGE")))
            Skip = "Set FANTASOCCER_TEST_STORAGE to the dedicated local Azurite account to verify shared Blob keys.";
    }
}

public sealed class McpBlobKeyRingTests
{
    [McpBlobFact]
    public async Task Independent_instances_share_encrypted_Blob_keys_and_survive_provider_restart()
    {
        var connection = Environment.GetEnvironmentVariable("FANTASOCCER_TEST_STORAGE")!;
        if (!connection.Contains("AccountName=migrationtest;", StringComparison.Ordinal) || !connection.Contains("127.0.0.1", StringComparison.Ordinal) && !connection.Contains("azurite", StringComparison.Ordinal))
            throw new InvalidOperationException("Only the dedicated local emulator is allowed.");
        // A private container owned by this test only: leaving even an empty mcp-keys behind pollutes the
        // export/restore suite, which enumerates the whole dedicated Azurite account on its next run.
        var container = new BlobContainerClient(connection, "keyring-test-" + Guid.NewGuid().ToString("N"));
        await container.CreateIfNotExistsAsync(cancellationToken: TestContext.Current.CancellationToken);
        var blob = container.GetBlobClient("test-keyring-" + Guid.NewGuid().ToString("N") + ".xml");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Fantasoccer OAuth keyring test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        ServiceProvider Instance()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDataProtection().SetApplicationName("Fantasoccer-keyring-test")
                .PersistKeysToAzureBlobStorage(blob).ProtectKeysWithCertificate(certificate);
            return services.BuildServiceProvider();
        }
        try
        {
            string protectedValue;
            using (var first = Instance())
                protectedValue = first.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-bearer").Protect("account-ticket");
            using var restarted = Instance();
            Assert.Equal("account-ticket", restarted.GetRequiredService<IDataProtectionProvider>().CreateProtector("test-bearer").Unprotect(protectedValue));
            var xml = (await blob.DownloadContentAsync(TestContext.Current.CancellationToken)).Value.Content.ToString();
            Assert.Contains("encryptedSecret", xml);
            Assert.DoesNotContain("<masterKey", xml);
        }
        finally { await container.DeleteIfExistsAsync(cancellationToken: TestContext.Current.CancellationToken); }
    }
}
