using System.Runtime.CompilerServices;
using Fantasoccer.StorageMigration;

namespace Fantasoccer.DataMigration;

// Structural coordinates and controlled reason codes only. Never include names,
// owner emails, original Blob names, derived player keys or full payloads.
public sealed record PlayerValidationDetails(string Container, string ArchiveReference, string JsonPath,
    string[] Reasons, bool NameMissing, int? NameLength, int? Role);

public sealed partial class CoreDatasetImporter
{
    private readonly record struct PlayerSource(LegacyJsonDocument Document, string Suffix, string ReaderFile, int ReaderLine)
    {
        public string JsonPath => (Document.Envelope.TryGetProperty("v", out _) ? "$.v" : "$.Value") + Suffix;
        public string Reader => Path.GetFileName(ReaderFile) + ":" + ReaderLine;
    }

    private static PlayerSource PlayerOrigin(LegacyJsonDocument document, string suffix,
        [CallerFilePath] string readerFile = "", [CallerLineNumber] int readerLine = 0)
        => new(document, suffix, readerFile, readerLine);

    private static string CheckedPlayerKey(RealPlayer? player, PlayerSource source)
    {
        var key = player?.Name is null ? null : player.Key;
        if (player is not null && !string.IsNullOrWhiteSpace(key) && player.Role is >= Fantasoccer.Role.Undefined and <= Fantasoccer.Role.Forward)
            return key;
        var reasons = new List<string>();
        if (player is null) reasons.Add("null_player");
        else
        {
            if (string.IsNullOrWhiteSpace(key)) reasons.Add("empty_player_key");
            if (player.Role is < Fantasoccer.Role.Undefined or > Fantasoccer.Role.Forward) reasons.Add("role_out_of_range");
        }
        throw new CoreImportException("invalid_player", "player", source.Reader, source.Document.Sha256,
            playerValidation: new(source.Document.Container, DocumentArchiveReference(source.Document), source.JsonPath,
                reasons.ToArray(), player?.Name is null, player?.Name?.Length, player is null ? null : (int)player.Role));
    }
}
