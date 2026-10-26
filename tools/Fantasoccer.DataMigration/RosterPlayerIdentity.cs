using System.Text.Json;
using Fantasoccer.Infrastructure.PostgreSql.Entities;

namespace Fantasoccer.DataMigration;

public sealed record RecoveredRosterPlayer(string DocumentSha256, string ArchiveReference, string JsonPath, int SeasonIndex, bool CreatedGlobalPlayer);

public sealed partial class CoreDatasetImporter
{
    private static void ValidateRosterPlayerReference(Player? player, JsonElement raw,
        IReadOnlyDictionary<string, FootballPlayer> known, PlayerSource origin)
    {
        if (player is null) { _ = CheckedPlayerKey(player, origin); return; }
        // In legacy Player JSON, compact "k" is the formation position, NOT
        // the player key. Only explicit identity properties are considered.
        var keys = raw.EnumerateObject().Where(x => x.Name.Equals("key", StringComparison.OrdinalIgnoreCase) ||
            x.Name.Equals("playerKey", StringComparison.OrdinalIgnoreCase)).ToArray();
        string? stored = null;
        foreach (var property in keys)
        {
            if (property.Value.ValueKind == JsonValueKind.Null) continue;
            if (property.Value.ValueKind != JsonValueKind.String) throw Conflict("invalid_stored_player_key");
            var value = property.Value.GetString();
            if (string.IsNullOrEmpty(value)) continue;
            if (value.Any(c => c is < 'a' or > 'z') || stored is not null && stored != value) throw Conflict("invalid_stored_player_key");
            stored = value;
        }
        if (string.IsNullOrWhiteSpace(player.Name) && stored is not null && known.TryGetValue(stored, out var existing))
            player.Name = existing.Name; // Verified key lookup, not a club/role/name guess; raw source remains unchanged.
        var expected = CheckedPlayerKey(player, origin);
        if (stored is not null && stored != expected) throw Conflict("player_key_name_mismatch");

        CoreImportException Conflict(string reason) => new("player_identity_conflict", "player", origin.Reader, origin.Document.Sha256,
            playerValidation: new(origin.Document.Container, DocumentArchiveReference(origin.Document), origin.JsonPath,
                [reason], player.Name is null, player.Name?.Length, (int)player.Role));
    }
}
