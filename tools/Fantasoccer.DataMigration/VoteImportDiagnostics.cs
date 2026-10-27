using System.Text.Json;

namespace Fantasoccer.DataMigration;

// Structural coordinates and controlled comparisons only. Never include player
// names/keys, clubs, original Blob names, vote values or raw payloads.
public sealed record VoteValidationDetails(string Container, string ArchiveReference, string JsonPath,
    int SeasonIndex, int Day, string[] Reasons, bool ValueIsFinite,
    string? OtherDocumentSha256 = null, string? OtherArchiveReference = null, string? OtherJsonPath = null,
    bool? NamesEqual = null, bool? NamesEqualIgnoringCase = null,
    bool? VotePayloadsEqual = null, bool? PlayerPayloadsEqual = null, bool? PlayerPayloadsByteEqual = null);

public sealed partial class CoreDatasetImporter
{
    private static CoreImportException VoteFault(string code, PlayerSource origin, int year, int day,
        VotedRealPlayer current, JsonElement rawCurrent, PlayerSource? otherOrigin = null,
        VotedRealPlayer? other = null, JsonElement? rawOther = null)
    {
        var finite = float.IsFinite(current.Vote.Value);
        var reasons = new List<string>();
        if (otherOrigin is not null) reasons.Add("duplicate_player_vote");
        if (!finite) reasons.Add("non_finite_vote_value");
        var comparison = origin.Document.Container == "pushnotificationsent" ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        JsonElement VotePayload(JsonElement sample) => sample.EnumerateObject().Last(x => string.Equals(x.Name, "v", comparison)).Value;
        return new(code, "vote", origin.Reader, origin.Document.Sha256,
            voteValidation: new(origin.Document.Container, DocumentArchiveReference(origin.Document), origin.JsonPath, year, day,
                reasons.ToArray(), finite, otherOrigin?.Document.Sha256,
                otherOrigin is { } previous ? DocumentArchiveReference(previous.Document) : null, otherOrigin?.JsonPath,
                other is null ? null : string.Equals(current.Name, other.Name, StringComparison.Ordinal),
                other is null ? null : string.Equals(current.Name, other.Name, StringComparison.OrdinalIgnoreCase),
                rawOther is { } voteRow ? JsonElement.DeepEquals(VotePayload(rawCurrent), VotePayload(voteRow)) : null,
                rawOther is { } row ? JsonElement.DeepEquals(rawCurrent, row) : null,
                rawOther is { } bytes ? string.Equals(rawCurrent.GetRawText(), bytes.GetRawText(), StringComparison.Ordinal) : null));
    }
}
