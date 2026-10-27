namespace Fantasoccer.DataMigration;

// Metadata coordinates only. These gaps are expected in minimal legacy vote
// feeds; neither a player name nor a club is inferred for the historical vote.
public sealed record MissingVoteClubDocument(string DocumentSha256, string Container, string ArchiveReference,
    int SeasonIndex, int Day, int Records, string FirstJsonPath);

public sealed partial class CoreDatasetImporter
{
    private static bool AllowsMissingVoteClub(PlayerSource source) => source.Document.Container is
        "live" or "notification" or "official" or "pushnotificationsent";

    private static string? VoteClub(RealPlayer sample, PlayerSource origin, Func<RealTeam?, ClubSource, string> club)
        => sample.RealTeam is null ? null : club(sample.RealTeam, ClubOrigin(origin));
}
