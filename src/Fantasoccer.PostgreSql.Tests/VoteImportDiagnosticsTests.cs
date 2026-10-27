using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    private const string VoteDiagnosticName = "private-vote-player-sentinel";
    private const string VoteDiagnosticBlob = "private-vote-owner@example.test";
    private static JsonObject DiagnosticVote(string? name = VoteDiagnosticName, bool hasVote = true) => new()
    {
        ["n"] = name, ["r"] = 3, ["a"] = false, ["t"] = null,
        ["v"] = new JsonObject { ["r"] = 3, ["v"] = 6, ["h"] = hasVote, ["i"] = true, ["g"] = 1 },
        ["unknown"] = "private-unknown-vote-sentinel"
    };

    [CoreFact]
    public async Task Duplicate_vote_diagnostics_preserve_original_coordinates_and_compare_raw_records_without_logging_values()
    {
        foreach (var source in new[] { "live", "notification", "official" })
        {
            var container = await ExtraContainerAsync(source);
            foreach (var historical in new[] { false, true })
            foreach (var kind in new[] { "identical", "case-and-flags", "normalized-key-collision", "unknown-vote-field" })
            {
                var first = DiagnosticVote(kind == "normalized-key-collision" ? VoteDiagnosticName + "-1" : VoteDiagnosticName);
                var second = (JsonObject)first.DeepClone();
                if (kind == "case-and-flags") { second["n"] = VoteDiagnosticName.ToUpperInvariant(); second["v"]!["h"] = false; }
                if (kind == "normalized-key-collision") second["n"] = VoteDiagnosticName + "-2";
                if (kind == "unknown-vote-field") second["v"]!["unknownVoteField"] = "private-extra-vote-sentinel";
                // Empty-name entries are skipped; these diagnostic ordinals
                // must still identify the originals at p[2] and p[4].
                var rows = new JsonArray(DiagnosticVote(""), DiagnosticVote("Different named fixture"), first, DiagnosticVote(" "), second).ToJsonString();
                rows = rows[1..^1];
                var json = LegacyVoteFixtureEnvelope(source, 12, rows, historical);
                var blob = container.GetBlobClient(VoteDiagnosticBlob);
                await blob.UploadAsync(BinaryData.FromString(json), overwrite: true); var etag = (await blob.GetPropertiesAsync()).Value.ETag;
                var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
                var hash = manifest.Containers.Single(x => x.Name == source).Blobs.Single(x => x.Name == VoteDiagnosticBlob).Sha256;
                await using var database = fixture.CreateContext();
                var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
                Assert.Equal("duplicate_or_invalid_vote", fault.Message); Assert.Equal("vote", fault.Field);
                Assert.StartsWith("FootballDataImport.cs:", fault.Reader); Assert.Equal(hash, fault.DocumentSha256);
                var details = Assert.IsType<VoteValidationDetails>(fault.VoteValidation);
                Assert.Equal(source, details.Container); Assert.Equal(12, details.SeasonIndex); Assert.Equal(1, details.Day);
                Assert.Equal(new[] { "duplicate_player_vote" }, details.Reasons); Assert.True(details.ValueIsFinite);
                Assert.Equal(historical ? "$.Value.p[4]" : "$.v.p[4]", details.JsonPath);
                Assert.Equal(historical ? "$.Value.p[2]" : "$.v.p[2]", details.OtherJsonPath);
                Assert.Equal(hash, details.OtherDocumentSha256); Assert.Equal(details.ArchiveReference, details.OtherArchiveReference);
                Assert.Equal(kind is "identical" or "unknown-vote-field", details.NamesEqual);
                Assert.Equal(kind != "normalized-key-collision", details.NamesEqualIgnoringCase);
                Assert.Equal(kind is "identical" or "normalized-key-collision", details.VotePayloadsEqual);
                Assert.Equal(kind == "identical", details.PlayerPayloadsEqual); Assert.Equal(kind == "identical", details.PlayerPayloadsByteEqual);
                var formatted = CoreImportDiagnostics.Format(fault);
                Assert.Contains("Vote details:", formatted); Assert.Contains(hash, formatted); Assert.Contains("No partial transaction was committed.", formatted);
                foreach (var secret in new[] { VoteDiagnosticName, VoteDiagnosticName.ToUpperInvariant(), KeyUtils.GetPlayerKey(VoteDiagnosticName),
                    VoteDiagnosticBlob, "private-unknown-vote-sentinel", "private-extra-vote-sentinel", "Different named fixture", "/root/" })
                    Assert.DoesNotContain(secret, formatted);
                var bytes = await File.ReadAllBytesAsync(Path.Combine(_dump, details.ArchiveReference));
                Assert.Equal(hash, Convert.ToHexStringLower(SHA256.HashData(bytes))); Assert.Equal(json, System.Text.Encoding.UTF8.GetString(bytes));
                Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag); Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
                Assert.Empty(await database.Users.ToArrayAsync()); Assert.Empty(await database.Players.ToArrayAsync()); Assert.Empty(await database.CoreImportCheckpoints.ToArrayAsync());
            }
            await container.DeleteBlobAsync(VoteDiagnosticBlob);
        }
    }

    [CoreFact]
    public async Task Non_finite_vote_diagnostics_distinguish_invalid_values_from_collisions_and_preserve_push_property_casing()
    {
        foreach (var source in new[] { "live", "notification", "official", "pushnotificationsent" })
        {
            var container = await ExtraContainerAsync(source);
            foreach (var historical in new[] { false, true })
            {
                var row = DiagnosticVote().ToJsonString().Replace("\"v\":6", "\"v\":1e100", StringComparison.Ordinal);
                var json = LegacyVoteFixtureEnvelope(source, 12, DiagnosticVote("").ToJsonString() + "," + row, historical);
                if (source == "pushnotificationsent") json = json.Replace("\"p\":[", "\"P\":[", StringComparison.Ordinal).Replace("\"t\":null,\"v\":{", "\"t\":null,\"V\":{", StringComparison.Ordinal);
                var blob = container.GetBlobClient(VoteDiagnosticBlob);
                await blob.UploadAsync(BinaryData.FromString(json), overwrite: true);
                var etag = (await blob.GetPropertiesAsync()).Value.ETag;
                await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
                await using var database = fixture.CreateContext();
                var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
                Assert.Equal(source == "pushnotificationsent" ? "invalid_push_baseline_vote" : "duplicate_or_invalid_vote", fault.Message);
                var details = Assert.IsType<VoteValidationDetails>(fault.VoteValidation);
                Assert.Equal(new[] { "non_finite_vote_value" }, details.Reasons); Assert.False(details.ValueIsFinite);
                var prefix = historical ? "$.Value" : "$.v";
                var suffix = source == "pushnotificationsent" ? (historical ? ".MatchEventVotes.P[1]" : ".matchEventVotes.P[1]") : ".p[1]";
                Assert.Equal(prefix + suffix, details.JsonPath); Assert.Null(details.OtherJsonPath); Assert.Null(details.NamesEqual); Assert.Null(details.VotePayloadsEqual);
                var formatted = CoreImportDiagnostics.Format(fault);
                Assert.Contains("non_finite_vote_value", formatted); Assert.DoesNotContain("duplicate_player_vote", formatted);
                Assert.DoesNotContain("1e100", formatted); Assert.DoesNotContain(VoteDiagnosticName, formatted); Assert.DoesNotContain(VoteDiagnosticBlob, formatted);
                Assert.Equal(etag, (await blob.GetPropertiesAsync()).Value.ETag); Assert.Equal(json, (await blob.DownloadContentAsync()).Value.Content.ToString());
                Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(_dump, details.ArchiveReference)));
                Assert.Empty(await database.CoreImportCheckpoints.ToArrayAsync()); Assert.Empty(await database.Users.ToArrayAsync());
            }
            await container.DeleteBlobAsync(VoteDiagnosticBlob);
        }
    }

    [CoreFact]
    public async Task Vote_diagnostics_keep_year_day_source_scope_and_still_refuse_duplicate_dataset_documents()
    {
        foreach (var source in new[] { "live", "notification", "official" })
        {
            var container = await ExtraContainerAsync(source);
            foreach (var year in new[] { 10, 12 })
            foreach (var day in new[] { 1, 2 })
                await container.GetBlobClient($"scope-{year}-{day}").UploadAsync(BinaryData.FromString(
                    LegacyVoteFixtureEnvelope(source, year, DiagnosticVote().ToJsonString(), historical: false)
                        .Replace("\"d\":1", "\"d\":" + day, StringComparison.Ordinal)));
        }
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(12, await database.FootballVotes.CountAsync()); Assert.Equal(1, await database.Players.CountAsync());
        Assert.Equal(2, await database.PlayerSeasons.CountAsync());
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump); Assert.Equal(report.Documents, repeated.SkippedDocuments);
        var duplicate = _storage.Blobs.GetBlobContainerClient("official").GetBlobClient("duplicate-dataset");
        await duplicate.UploadAsync(BinaryData.FromString(LegacyVoteFixtureEnvelope("official", 12, DiagnosticVote().ToJsonString(), historical: false)));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => new CoreDatasetImporter(database).ImportAsync(_dump));
        Assert.Equal("duplicate_football_dataset", fault.Message); Assert.Null(fault.VoteValidation);
        Assert.Equal(12, await database.FootballVotes.CountAsync());
    }

    [CoreFact]
    public async Task Duplicate_vote_with_a_non_finite_value_reports_both_reasons_instead_of_arbitrarily_choosing_one()
    {
        var source = await ExtraContainerAsync("official");
        var first = DiagnosticVote().ToJsonString();
        var second = first.Replace("\"v\":6", "\"v\":1e100", StringComparison.Ordinal);
        await source.GetBlobClient(VoteDiagnosticBlob).UploadAsync(BinaryData.FromString(LegacyVoteFixtureEnvelope("official", 12, first + "," + second, historical: true)));
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var fault = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        var details = Assert.IsType<VoteValidationDetails>(fault.VoteValidation);
        Assert.Equal(new[] { "duplicate_player_vote", "non_finite_vote_value" }, details.Reasons);
        Assert.False(details.ValueIsFinite); Assert.False(details.VotePayloadsEqual); Assert.True(details.NamesEqual);
    }
}
