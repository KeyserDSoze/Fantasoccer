using System.Text.Json;

namespace Fantasoccer.DataMigration;

// Operator-approved exclusions from SQL; the original bytes stay in the
// verified dump. Reports carry structural coordinates and counts, plus the
// economic values of excluded roster entries, never names or payloads.
public sealed record SkippedUnusablePlayerRowDocument(string DocumentSha256, string Container, string ArchiveReference,
    int SeasonIndex, int Day, int TotalEntries, int Records, string FirstJsonPath);

public sealed record ExcludedNamelessRosterEntry(string DocumentSha256, string Container, string ArchiveReference, string JsonPath,
    int SeasonIndex, int Day, int Price, int Revenue, int Status, int Position);

public sealed partial class CoreDatasetImporter
{
    // A missing name, or one without any letter that forms a player key (for
    // example "----"), cannot identify anyone. Null entries and invalid roles
    // keep their existing diagnostics.
    private static bool UnusablePlayerName(RealPlayer? player)
        => player is not null && (string.IsNullOrWhiteSpace(player.Name) || string.IsNullOrEmpty(player.Key));

    // A verified stored key can still recover a roster name; see
    // ValidateRosterPlayerReference. Such entries are never excluded here.
    private static bool HasStoredPlayerKey(JsonElement raw)
        => raw.ValueKind == JsonValueKind.Object && raw.EnumerateObject().Any(x =>
            (x.Name.Equals("key", StringComparison.OrdinalIgnoreCase) || x.Name.Equals("playerKey", StringComparison.OrdinalIgnoreCase)) &&
            x.Value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(x.Value.GetString()));

    // Only sold or removed entries are excluded: an active nameless player
    // would change a lineup in use and still needs review.
    private static bool ExcludedNamelessRosterPlayer(Player? player, JsonElement raw)
        => player is not null && UnusablePlayerName(player) && player.Status is > PlayerInTeamStatus.Active and <= PlayerInTeamStatus.Removed && !HasStoredPlayerKey(raw);
}
