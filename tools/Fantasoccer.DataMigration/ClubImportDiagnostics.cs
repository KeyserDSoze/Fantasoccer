using System.Runtime.CompilerServices;
using Fantasoccer.StorageMigration;

namespace Fantasoccer.DataMigration;

// Only structural coordinates, lengths and controlled reasons. Never log the
// club name/code, player, owner, original Blob name or rejected JSON payload.
public sealed record ClubValidationDetails(string Container, string ArchiveReference, string JsonPath,
    string[] Reasons, bool NameMissing, int? NameLength, bool AbbreviationMissing, int? AbbreviationLength);

public sealed partial class CoreDatasetImporter
{
    private readonly record struct ClubSource(LegacyJsonDocument Document, string Suffix, string ReaderFile, int ReaderLine)
    {
        public string JsonPath => (Document.Envelope.TryGetProperty("v", out _) ? "$.v" : "$.Value") + Suffix;
        public string Reader => Path.GetFileName(ReaderFile) + ":" + ReaderLine;
    }

    private static ClubSource ClubOrigin(LegacyJsonDocument document, string suffix,
        [CallerFilePath] string readerFile = "", [CallerLineNumber] int readerLine = 0)
        => new(document, suffix, readerFile, readerLine);

    private static ClubSource ClubOrigin(PlayerSource source)
    {
        // Push baselines also accept historical casing. Keep the actual source
        // spelling, just as their outer player-array coordinate does.
        var clubProperty = "t";
        if (source.Document.Container == "pushnotificationsent")
        {
            var raw = Value(source.Document.Envelope);
            foreach (var part in source.Suffix.Split('.', StringSplitOptions.RemoveEmptyEntries))
            {
                var bracket = part.IndexOf('[');
                raw = raw.GetProperty(bracket < 0 ? part : part[..bracket]);
                if (bracket >= 0) raw = raw[int.Parse(part[(bracket + 1)..^1], System.Globalization.CultureInfo.InvariantCulture)];
            }
            if (raw.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var properties = raw.EnumerateObject().Where(x => x.Name.Equals("t", StringComparison.OrdinalIgnoreCase)).ToArray();
                if (properties.Length > 0) clubProperty = properties[^1].Name;
            }
        }
        return new(source.Document, source.Suffix + "." + clubProperty, source.ReaderFile, source.ReaderLine);
    }

    private static string CheckedClubCode(RealTeam? club, ClubSource source)
    {
        var abbreviation = club?.ExplicitAbbreviation;
        var reasons = new List<string>();
        var code = "invalid_club_code";
        if (club?.Name is not { Length: > 0 })
        {
            code = "missing_real_club";
            reasons.Add(club is null ? "null_club" : "empty_club_name");
        }
        else
        {
            try { return KeyUtils.GetClubKey(abbreviation, club.Name); }
            catch (ArgumentException)
            {
                var candidate = string.IsNullOrWhiteSpace(abbreviation) && club.Name.Length == 3 ? club.Name : abbreviation;
                if (string.IsNullOrWhiteSpace(candidate)) reasons.Add("missing_club_code");
                else
                {
                    if (candidate.Length != 3) reasons.Add("club_code_length");
                    if (candidate.Any(c => !char.IsAsciiLetter(c))) reasons.Add("club_code_not_ascii_letters");
                }
            }
        }
        throw new CoreImportException(code, "club", source.Reader, source.Document.Sha256,
            clubValidation: new(source.Document.Container, DocumentArchiveReference(source.Document), source.JsonPath,
                reasons.ToArray(), club?.Name is null, club?.Name?.Length, string.IsNullOrWhiteSpace(abbreviation), abbreviation?.Length));
    }
}
