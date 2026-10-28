using Fantasoccer.DataMigration;
using Fantasoccer.StorageMigration;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Markets_saved_with_the_2024_2025_long_names_are_excluded_and_reported_while_a_missing_list_is_still_refused()
    {
        var markets = await ExtraContainerAsync("marketwrapper");
        async Task PutAsync(string value) => await markets.GetBlobClient("market").UploadAsync(BinaryData.FromString(
            "{\"Key\":{\"g\":\"first\",\"l\":\"league\",\"y\":15},\"Value\":" + value + "}"), overwrite: true);
        await PutAsync("{}");
        await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var missing = await Assert.ThrowsAsync<CoreImportException>(() => CoreDatasetImporter.AnalyzeAsync(_dump));
        Assert.Equal(("missing_market_trades", "marketwrapper $.Value.m"), (missing.Message, missing.Field));

        // The model in use from 6 October 2024 to 4 October 2025 had no short names.
        await PutAsync("{\"Markets\":[{\"Id\":\"legacy\",\"Buyer\":\"" + Email + "\",\"Seller\":\"" + Email + "\",\"SellerPlayers\":[],\"BuyerPlayers\":[]," +
            "\"MoneyFromBuyer\":5,\"MoneyFromSeller\":0,\"Approvers\":[],\"Deniers\":[],\"Status\":1,\"CreationTime\":\"2024-11-02T10:00:00\"}]}");
        var manifest = await new StorageExporter(_storage).ExportAsync(_dump, resume: true);
        var hash = manifest.Containers.Single(x => x.Name == "marketwrapper").Blobs.Single().Sha256;
        await using var database = fixture.CreateContext();
        var report = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(44, report.MappingVersion);
        var excluded = Assert.Single(report.ExcludedLegacyMarkets!);
        Assert.Equal((hash, 15, 1), (excluded.DocumentSha256, excluded.SeasonIndex, excluded.Entries));
        Assert.StartsWith("blobs/", excluded.ArchiveReference);
        Assert.Contains(report.PreservedArtifacts!, x => x.Contains(hash, StringComparison.Ordinal));
        Assert.Contains(report.PendingFields, x => x.Contains("excludedLegacyMarkets", StringComparison.Ordinal));
        Assert.Empty(await database.MarketBooks.ToArrayAsync());
        Assert.Empty(await database.MarketTrades.ToArrayAsync());
        Assert.DoesNotContain(Email, System.Text.Json.JsonSerializer.Serialize(report.ExcludedLegacyMarkets), StringComparison.OrdinalIgnoreCase);
        var repeated = await new CoreDatasetImporter(database).ImportAsync(_dump);
        Assert.Equal(repeated.Documents, repeated.SkippedDocuments);
    }
}
