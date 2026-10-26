using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Identity_uses_the_verified_Blob_lookup_email_when_embedded_key_is_opaque_missing_or_empty_and_preserves_account_roles()
    {
        var blob = _storage.Blobs.GetBlobContainerClient("appidentity").GetBlobClient(Email);
        var original = JsonSerializer.Deserialize<Entity<AppIdentity, string>>((await blob.DownloadContentAsync()).Value.Content.ToString())!;
        var account = original.Value!;
        account.Email = Email.ToUpperInvariant();
        await using var database = fixture.CreateContext();
        Guid? stableId = null;
        foreach (var key in new string?[] { "opaque-legacy-key-not-an-email", null, "" })
        {
            await WriteIdentityEnvelopeAsync(Email, key, account);
            var sourceEtag = (await blob.GetPropertiesAsync()).Value.ETag;
            // This is precisely the old point reader's behavior: open by email
            // and deserialize Value, independently of the embedded Key.
            var legacyValue = JsonSerializer.Deserialize<Entity<AppIdentity, string>>((await blob.DownloadContentAsync()).Value.Content.ToString())!.Value;
            Assert.Equal(account.Email, legacyValue!.Email);
            var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
            var sourceHash = manifest.Containers.Single(x => x.Name == "appidentity").Blobs.Single(x => x.Name == Email).Sha256;
            var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Equal(sourceHash, Assert.Single(report.ReconciledIdentityDocuments!));
            Assert.Contains(report.PendingFields, x => x.Contains("unusable embedded keys"));
            var user = await database.Users.AsNoTracking().SingleAsync(x => x.Email == Email);
            Assert.True(user.IsEnabled); Assert.False(user.IsHistoricalPlaceholder);
            Assert.Equal("Friend", user.DisplayName);
            stableId ??= user.Id;
            Assert.Equal(stableId, user.Id);
            var roles = await database.GroupMemberships.Where(x => x.UserId == user.Id).Select(x => x.Roles).ToArrayAsync();
            Assert.Equal(2, roles.Length); Assert.Contains(GroupRole.Admin, roles); Assert.Contains(GroupRole.Participant, roles);
            Assert.DoesNotContain(roles, x => (x & GroupRole.SuperAdmin) != 0);
            var audit = await database.LegacyIdentityMaps.SingleAsync(x => x.Kind == "identity-document" && x.TargetId == user.Id);
            Assert.Contains(sourceHash, audit.LegacyKey);
            if (!string.IsNullOrEmpty(key)) Assert.DoesNotContain(key, audit.LegacyKey + JsonSerializer.Serialize(report));
            Assert.Equal(sourceEtag, (await blob.GetPropertiesAsync()).Value.ETag);
            Assert.Equal(key, JsonSerializer.Deserialize<Entity<AppIdentity, string>>((await blob.DownloadContentAsync()).Value.Content.ToString())!.Key);
            var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
            Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
            Assert.Equal(sourceHash, Assert.Single(repeated.ReconciledIdentityDocuments!));
        }
    }

    [CoreFact]
    public async Task Opaque_identity_key_without_a_matching_Blob_lookup_remains_blocked_and_reports_only_a_document_hash()
    {
        const string opaque = "private-opaque-key-not-for-logs";
        await WriteIdentityEnvelopeAsync("unresolved-record", opaque, new() { Email = "incoming@example.test", Username = "Incoming", Groups = [] });
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var hash = manifest.Containers.Single(x => x.Name == "appidentity").Blobs.Single(x => x.Name == "unresolved-record").Sha256;
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("invalid_email", fault.Message); Assert.Equal("key", fault.Field); Assert.Equal(hash, fault.DocumentSha256);
        Assert.DoesNotContain(opaque, fault.ToString() + fault.Field + fault.Reader + fault.DocumentSha256);
        await using var database = fixture.CreateContext(); Assert.Empty(await database.Users.ToArrayAsync());
    }

    [CoreFact]
    public async Task Valid_but_conflicting_identity_keys_and_conflicting_Blob_lookup_names_are_never_reassigned_to_the_profile_email()
    {
        var blob = _storage.Blobs.GetBlobContainerClient("appidentity").GetBlobClient(Email);
        var account = JsonSerializer.Deserialize<Entity<AppIdentity, string>>((await blob.DownloadContentAsync()).Value.Content.ToString())!.Value!;
        await WriteIdentityEnvelopeAsync(Email, OtherEmail, account);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var keyConflict = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("identity_collision", keyConflict.Message); Assert.NotNull(keyConflict.DocumentSha256);
        await WriteIdentityEnvelopeAsync(Email, Email, account);
        await WriteIdentityEnvelopeAsync(OtherEmail, "opaque-old-key", account);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var lookupConflict = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("identity_collision", lookupConflict.Message); Assert.NotNull(lookupConflict.DocumentSha256);
        await using var database = fixture.CreateContext(); Assert.Empty(await database.Users.ToArrayAsync());
    }

    [CoreFact]
    public async Task Matching_lookup_fallback_never_silently_merges_duplicate_identity_documents()
    {
        var blob = _storage.Blobs.GetBlobContainerClient("appidentity").GetBlobClient(Email);
        var account = JsonSerializer.Deserialize<Entity<AppIdentity, string>>((await blob.DownloadContentAsync()).Value.Content.ToString())!.Value!;
        await WriteIdentityEnvelopeAsync(Email.ToUpperInvariant(), "another-opaque-key", account);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal("identity_collision", fault.Message); Assert.NotNull(fault.DocumentSha256);
        await using var database = fixture.CreateContext(); Assert.Empty(await database.Users.ToArrayAsync());
    }

    private async Task WriteIdentityEnvelopeAsync(string blobName, string? key, AppIdentity account)
        => await _storage.Blobs.GetBlobContainerClient("appidentity").GetBlobClient(blobName)
            .UploadAsync(BinaryData.FromString(JsonSerializer.Serialize(new Entity<AppIdentity, string>(account, key))), overwrite: true);
}
