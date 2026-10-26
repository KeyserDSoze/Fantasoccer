using System.Text.Json;
using Fantasoccer.DataMigration;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using RepositoryFramework;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class IdentityResolutionConfigurationTests
{
    [Fact]
    public void Resolution_config_is_empty_by_default_and_accepts_only_valid_nonoverlapping_hash_pairs()
    {
        Assert.Empty(IdentityResolutionConfiguration.Parse(null)); Assert.Empty(IdentityResolutionConfiguration.Parse(""));
        Assert.Empty(IdentityResolutionConfiguration.Parse("[]"));
        var preferred = new string('A', 64); var other = new string('b', 64);
        var rules = IdentityResolutionConfiguration.Parse(JsonSerializer.Serialize(new[] { new IdentityDocumentResolution(preferred, other) }));
        Assert.Equal(preferred.ToLowerInvariant(), Assert.Single(rules).PreferredDocumentSha256);
        Assert.Equal(other, rules[0].OtherDocumentSha256);
        foreach (var invalid in new[] { "null", "{}", "[{}]", "[{\"preferredDocumentSha256\":\"private-invalid-value\",\"otherDocumentSha256\":\"bad\"}]", "[{\"unexpected\":true}]" })
        {
            var fault = Assert.Throws<CoreImportException>(() => IdentityResolutionConfiguration.Parse(invalid));
            Assert.Equal("invalid_identity_resolution_config", fault.Message);
            Assert.DoesNotContain("private-invalid-value", CoreImportDiagnostics.Format(fault));
        }
        Assert.Throws<CoreImportException>(() => IdentityResolutionConfiguration.Normalize([new(preferred, preferred)]));
        Assert.Throws<CoreImportException>(() => IdentityResolutionConfiguration.Normalize([new(preferred, other), new(other, new string('c', 64))]));
    }
}

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Explicit_hash_resolution_chooses_only_the_email_lookup_profile_without_merging_roles_or_selections_and_is_idempotent()
    {
        await using var database = fixture.CreateContext();
        await new CoreDatasetImporter(database).ImportAsync(_dump);
        var originalId = (await database.Users.AsNoTracking().SingleAsync(x => x.Email == Email)).Id;
        var container = _storage.Blobs.GetBlobContainerClient("appidentity");
        var preferred = JsonSerializer.Deserialize<Entity<AppIdentity, string>>((await container.GetBlobClient(Email).DownloadContentAsync()).Value.Content.ToString())!.Value!;
        var alias = new AppIdentity { Email = Email, Username = preferred.Username,
            State = new() { GroupId = "second", LeagueId = "league", Year = 15 },
            Groups = preferred.Groups.Select(group => new GroupForIdentity { Id = group.Id, Name = group.Name, Role = IdentityRole.SuperAdmin | IdentityRole.Reader }).ToList() };
        await WriteIdentityEnvelopeAsync("legacy0001", "legacy0001", alias);
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var rules = Resolutions(manifest, Email, "legacy0001");
        var options = new CoreImportOptions(IdentityResolutions: rules);
        var preferredEtag = (await container.GetBlobClient(Email).GetPropertiesAsync()).Value.ETag;
        var otherEtag = (await container.GetBlobClient("legacy0001").GetPropertiesAsync()).Value.ETag;
        var report = await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        var applied = Assert.Single(report.ResolvedIdentityConflicts!);
        Assert.Equal(rules[0].PreferredDocumentSha256, applied.PreferredDocumentSha256);
        Assert.Equal(rules[0].OtherDocumentSha256, applied.OtherDocumentSha256);
        Assert.Contains(report.PendingFields, field => field.Contains("Roles and selections were not merged"));
        Assert.Contains(report.PreservedArtifacts!, field => field.Contains(applied.OtherDocumentSha256) && field.Contains("unselected duplicate"));
        Assert.Equal(2, await database.Users.CountAsync());
        var user = await database.Users.AsNoTracking().SingleAsync(x => x.Email == Email);
        Assert.Equal(originalId, user.Id); Assert.Equal(preferred.Username, user.DisplayName);
        var selection = await database.AccountSelections.SingleAsync(x => x.UserId == user.Id);
        Assert.Null(selection.GroupId); Assert.Null(selection.LeagueId); Assert.Null(selection.Year);
        var roles = await database.GroupMemberships.Where(x => x.UserId == user.Id).Select(x => x.Roles).ToArrayAsync();
        Assert.Contains(GroupRole.Admin, roles); Assert.Contains(GroupRole.Participant, roles);
        Assert.DoesNotContain(roles, role => (role & (GroupRole.SuperAdmin | GroupRole.Reader)) != 0);
        Assert.Empty(await database.ExternalLogins.ToArrayAsync());
        var audit = await database.LegacyIdentityMaps.SingleAsync(x => x.Kind == "identity-resolution" && x.TargetId == user.Id);
        Assert.Contains(applied.PreferredDocumentSha256, audit.LegacyKey); Assert.Contains(applied.OtherDocumentSha256, audit.LegacyKey);
        Assert.DoesNotContain("legacy0001", audit.LegacyKey + JsonSerializer.Serialize(report));
        Assert.Equal(preferredEtag, (await container.GetBlobClient(Email).GetPropertiesAsync()).Value.ETag);
        Assert.Equal(otherEtag, (await container.GetBlobClient("legacy0001").GetPropertiesAsync()).Value.ETag);
        Assert.Equal(2, manifest.Containers.Single(x => x.Name == "appidentity").Blobs.Count(blob => blob.Name == Email || blob.Name == "legacy0001"));
        var repeated = await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments); Assert.Single(repeated.ResolvedIdentityConflicts!);
        var withoutApproval = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
        Assert.Equal("identity_collision", withoutApproval.Message);

        // Content pins are intentionally stale after any change; a previous
        // approval must not silently choose a changed account profile.
        preferred.Username = "Changed after approval";
        await WriteIdentityEnvelopeAsync(Email, Email, preferred);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var stale = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database, options).ImportAsync(_dump));
        Assert.Equal("identity_resolution_document_not_found", stale.Message);
        Assert.Equal("Friend", (await database.Users.AsNoTracking().SingleAsync(x => x.Email == Email)).DisplayName);
    }

    [CoreFact]
    public async Task Approved_identity_resolution_does_not_reopen_import_after_application_writes()
    {
        var container = _storage.Blobs.GetBlobContainerClient("appidentity");
        var profile = JsonSerializer.Deserialize<Entity<AppIdentity, string>>((await container.GetBlobClient(Email).DownloadContentAsync()).Value.Content.ToString())!.Value!;
        await WriteIdentityEnvelopeAsync("legacy0001", "legacy0001", profile);
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var options = new CoreImportOptions(IdentityResolutions: Resolutions(manifest, Email, "legacy0001"));
        await using var database = fixture.CreateContext();
        await new CoreDatasetImporter(database, options).ImportAsync(_dump);
        await using (var interactive = fixture.CreateContext())
        {
            await using var transaction = await Fantasoccer.Infrastructure.PostgreSql.CoreMigrationLock.BeginInteractiveWriteAsync(interactive, default);
            await interactive.SaveChangesAsync(); await transaction.CommitAsync();
        }
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database, options).ImportAsync(_dump));
        Assert.Equal("interactive_writes_already_started", fault.Message);
        Assert.Equal(2, await database.Users.CountAsync());
    }

    [CoreFact]
    public async Task Identity_resolution_refuses_reversed_missing_unrelated_and_third_document_choices()
    {
        var container = _storage.Blobs.GetBlobContainerClient("appidentity");
        var profile = JsonSerializer.Deserialize<Entity<AppIdentity, string>>((await container.GetBlobClient(Email).DownloadContentAsync()).Value.Content.ToString())!.Value!;
        await WriteIdentityEnvelopeAsync("legacy0001", "legacy0001", profile);
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var rule = Resolutions(manifest, Email, "legacy0001")[0];
        var otherUserHash = manifest.Containers.Single(x => x.Name == "appidentity").Blobs.Single(x => x.Name == OtherEmail).Sha256;
        foreach (var (choice, expected) in new[]
        {
            (new IdentityDocumentResolution(rule.OtherDocumentSha256, rule.PreferredDocumentSha256), "identity_resolution_lookup_required"),
            (new IdentityDocumentResolution(new string('0', 64), rule.OtherDocumentSha256), "identity_resolution_document_not_found"),
            (new IdentityDocumentResolution(otherUserHash, rule.OtherDocumentSha256), "identity_resolution_email_mismatch")
        })
        {
            var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump, options: new(IdentityResolutions: [choice])));
            Assert.Equal(expected, fault.Message);
        }
        await WriteIdentityEnvelopeAsync("legacy0002", "legacy0002", profile);
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var third = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump, options: new(IdentityResolutions: [rule])));
        Assert.Equal("identity_resolution_ambiguous", third.Message);
        await using var database = fixture.CreateContext(); Assert.Empty(await database.Users.ToArrayAsync());
    }

    [CoreFact]
    public async Task Identity_resolution_never_bypasses_authoritative_group_membership_checks()
    {
        var container = _storage.Blobs.GetBlobContainerClient("appidentity");
        var correct = JsonSerializer.Deserialize<Entity<AppIdentity, string>>((await container.GetBlobClient(Email).DownloadContentAsync()).Value.Content.ToString())!.Value!;
        await WriteIdentityEnvelopeAsync("legacy0001", "legacy0001", correct);
        var selected = new AppIdentity { Email = Email, Username = correct.Username, State = correct.State,
            Groups = correct.Groups.Select(group => new GroupForIdentity { Id = group.Id, Name = group.Name, Role = IdentityRole.Reader }).ToList() };
        await WriteIdentityEnvelopeAsync(Email, Email, selected);
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump,
            options: new(IdentityResolutions: Resolutions(manifest, Email, "legacy0001"))));
        Assert.Equal("membership_conflict", fault.Message);
        await using var database = fixture.CreateContext(); Assert.Empty(await database.Users.ToArrayAsync());
    }

    [CoreFact]
    public async Task Identity_resolution_requires_unique_content_hashes_and_cannot_choose_between_email_lookup_aliases()
    {
        var container = _storage.Blobs.GetBlobContainerClient("appidentity");
        var profile = JsonSerializer.Deserialize<Entity<AppIdentity, string>>((await container.GetBlobClient(Email).DownloadContentAsync()).Value.Content.ToString())!.Value!;
        await WriteIdentityEnvelopeAsync("legacy0001", "legacy0001", profile);
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var rule = Resolutions(manifest, Email, "legacy0001")[0];
        await WriteIdentityEnvelopeAsync(Email.ToUpperInvariant(), Email, profile); // Same content hash, different Blob.
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var ambiguous = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump, options: new(IdentityResolutions: [rule])));
        Assert.Equal("identity_resolution_ambiguous", ambiguous.Message);
        await container.DeleteBlobAsync("legacy0001");
        await WriteIdentityEnvelopeAsync(Email.ToUpperInvariant(), Email.ToUpperInvariant(), profile); // Distinct content hashes, both email lookups.
        manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var aliases = Resolutions(manifest, Email, Email.ToUpperInvariant());
        var refused = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump, options: new(IdentityResolutions: aliases)));
        Assert.Equal("identity_resolution_lookup_required", refused.Message);
    }

    private static IdentityDocumentResolution[] Resolutions(DumpManifest manifest, string preferredName, string otherName)
    {
        var blobs = manifest.Containers.Single(x => x.Name == "appidentity").Blobs;
        return [new(blobs.Single(x => x.Name == preferredName).Sha256, blobs.Single(x => x.Name == otherName).Sha256)];
    }
}
