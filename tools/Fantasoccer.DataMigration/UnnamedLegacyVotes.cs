namespace Fantasoccer.DataMigration;

// Operator-approved exclusion from SQL, not an unresolved-vote archive. The
// original bytes stay in the verified dump. Report only structural coordinates
// and counts, never player/club labels or payloads.
public sealed record SkippedUnnamedVoteDocument(string DocumentSha256, string Container, string ArchiveReference,
    int SeasonIndex, int Day, int TotalEntries, int Records, int WithHasVote, string FirstJsonPath);

public sealed partial class CoreDatasetImporter
{
    // Keep null entries and nonempty names with invalid normalized keys subject
    // to the existing diagnostics. This is not a generic invalid-player skip.
    private static bool IsUnnamedVote(VotedRealPlayer? sample)
        => sample is not null && string.IsNullOrWhiteSpace(sample.Name);
}
