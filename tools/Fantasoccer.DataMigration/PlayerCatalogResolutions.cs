using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.StorageMigration;

namespace Fantasoccer.DataMigration;

public sealed record PlayerCatalogResolution(string DocumentSha256, int ExpectedUnnamedPlayers);
public sealed record AppliedPlayerCatalogResolution(string DocumentSha256, string ArchiveReference, int SeasonIndex,
    int TotalEntries, int ImportedEntries, int UnresolvedEntries, bool ExplicitApproval = false);

public static class PlayerCatalogResolutionConfiguration
{
    public static IReadOnlyList<PlayerCatalogResolution> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        if (json.Length > 32768) throw Invalid();
        try
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
            return Normalize(JsonSerializer.Deserialize<PlayerCatalogResolution[]>(json, options) ?? throw Invalid());
        }
        catch (JsonException) { throw Invalid(); }
    }

    public static IReadOnlyList<PlayerCatalogResolution> Normalize(IReadOnlyList<PlayerCatalogResolution>? rules)
    {
        if (rules is null) return [];
        if (rules.Count > 100) throw Invalid();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<PlayerCatalogResolution>();
        foreach (var rule in rules)
        {
            if (rule is null || rule.DocumentSha256 is not { Length: 64 } hash || !hash.All(char.IsAsciiHexDigit) ||
                rule.ExpectedUnnamedPlayers is < 1 or > 100000) throw Invalid();
            hash = hash.ToLowerInvariant();
            if (!used.Add(hash)) throw Invalid();
            result.Add(rule with { DocumentSha256 = hash });
        }
        return result.AsReadOnly();
    }
    private static CoreImportException Invalid() => new("invalid_player_catalog_resolution_config");
}

public sealed partial class CoreDatasetImporter
{
    private static IReadOnlyDictionary<string, PlayerCatalogResolution> PreparePlayerCatalogResolutions(
        VerifiedJsonDataset dataset, IReadOnlyList<PlayerCatalogResolution>? configuredRules)
    {
        var rules = PlayerCatalogResolutionConfiguration.Normalize(configuredRules);
        foreach (var rule in rules)
        {
            var matches = dataset.Documents.Where(x => x.Container == "realplayerswrapper" && x.Sha256 == rule.DocumentSha256).ToArray();
            if (matches.Length == 0) throw new CoreImportException("player_catalog_resolution_document_not_found");
            if (matches.Length != 1) throw new CoreImportException("player_catalog_resolution_ambiguous", documentSha256: rule.DocumentSha256);
        }
        return rules.ToDictionary(x => x.DocumentSha256, StringComparer.Ordinal);
    }

    private static void PreserveUnnamedCatalogEntry(VerifiedJsonDataset dataset, CorePlan plan, LegacyJsonDocument document,
        RealPlayer? player, int year, int index, Func<string, string, Guid> map)
    {
        var origin = PlayerOrigin(document, $".p[{index}]");
        var raw = Value(document.Envelope).GetProperty("p")[index];
        plan.UnresolvedPlayerCatalogEntries.Add(new()
        {
            Id = map("unresolved-player-catalog-entry", Composite(document.Sha256, index.ToString(CultureInfo.InvariantCulture))),
            SourceId = dataset.Manifest.SourceId, DocumentSha256 = document.Sha256, ArchiveReference = DocumentArchiveReference(document),
            JsonPath = origin.JsonPath, SourceOrdinal = index, SeasonIndex = year, Reason = "empty_player_name", OriginalPayloadJson = raw.GetRawText()
        });
    }
}
