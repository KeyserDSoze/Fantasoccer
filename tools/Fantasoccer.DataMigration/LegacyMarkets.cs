using System.Text.Json;
using Fantasoccer.StorageMigration;

namespace Fantasoccer.DataMigration;

// Structural coordinates and the number of stored entries only.
public sealed record ExcludedLegacyMarketDocument(string DocumentSha256, string ArchiveReference, int SeasonIndex, int Entries);

public sealed partial class CoreDatasetImporter
{
    // From 6 October 2024 to 4 October 2025 the market model had no short JSON
    // names: the trades were saved under "Markets" with "Id", "Buyer", ... and
    // were never converted (the legacy app could no longer read them). By
    // operator decision such documents are not imported; their bytes stay in
    // the verified dump. A document with neither list is still refused.
    private static ExcludedLegacyMarketDocument? LegacyMarketFormat(LegacyJsonDocument document, int seasonIndex)
    {
        var value = Value(document.Envelope);
        if (value.ValueKind != JsonValueKind.Object || value.TryGetProperty("m", out _) || !value.TryGetProperty("Markets", out var markets)) return null;
        return new(document.Sha256, DocumentArchiveReference(document), seasonIndex, markets.ValueKind == JsonValueKind.Array ? markets.GetArrayLength() : 0);
    }
}
