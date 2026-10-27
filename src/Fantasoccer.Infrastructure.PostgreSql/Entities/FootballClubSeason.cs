namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

[Flags]
public enum ClubMembershipSource
{
    None = 0,
    Catalogue = 1,
    Calendar = 2,
    Standings = 4
}

// Confirmed Serie A membership, not every club mentioned by an old roster or
// inactive player. Player/snapshot ClubCode FKs still target the global club.
public sealed class FootballClubSeason
{
    public const string CatalogueDatasetKind = "clubseasoncatalogue";
    public string ClubId { get; set; } = null!;
    public int SeasonIndex { get; set; }
    public ClubMembershipSource Sources { get; set; }
    public FootballClub Club { get; set; } = null!;
    public Season Season { get; set; } = null!;
}
