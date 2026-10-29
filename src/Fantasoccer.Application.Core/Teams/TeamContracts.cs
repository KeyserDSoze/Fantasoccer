namespace Fantasoccer.Application.Core.Teams;

public sealed record FootballClubDto(string Name, string Abbreviation)
{
    public string ClubCode => Abbreviation;
    // Legacy snapshots without a recorded club keep NULL in SQL; clients get the
    // same empty club the legacy documents contained, never an invented one.
    public static FootballClubDto Of(Fantasoccer.Infrastructure.PostgreSql.Entities.FootballClub? club) => club is null ? new("", "") : new(club.Name, club.Id);
}
public sealed record RosterPlayerDto(string PlayerId, int Ordinal, string Name, FootballClubDto Team, int Role, bool IsActive,
    bool Visible, int Price, int Revenue, int Status, int Position)
{
    public int? SeasonIndex { get; init; }
    public string PlayerKey => PlayerId;
}
public sealed record FormationPlayerChangeDto(string Name, int FromPosition, int ToPosition);
public sealed record FormationChangeDto(string? Date, string Author, FormationPlayerChangeDto[] Players);
public sealed record TeamRosterDto(Guid TeamId, Guid GroupId, Guid BasketId, int SeasonIndex, int Kind, int Day,
    Guid? LeagueId, string Name, string Owner, string[] AdditionalOwners, RosterPlayerDto[] Players,
    int MoneyFromRank, int OpeningCompetitionPrize, string? LastUpdate, int FormationChanges, FormationChangeDto[] FormationChangeHistory, int Version, string? LogoUrl = null, int? SourceVersion = null,
    int MarketBalanceAdjustment = 0, int? RankCreditGranted = null)
{
    // Lineup entries whose player is no longer in the roster (sold after saving):
    // reported, never silently corrected.
    public string[] UnavailablePlayerIds { get; init; } = [];
    // Day whose lineup is proposed (not saved) because this day has none yet.
    public int? ProposedFromDay { get; init; }
}
public sealed record FormationPositionRequest(string PlayerId, int Position);
public sealed record SaveRosterFormationRequest(Guid GroupId, Guid BasketId, int Year, string Owner, int Day,
    Guid? LeagueId, int Kind, FormationPositionRequest[] Positions, int ExpectedVersion, bool Live = false, bool AsAdmin = false, Guid? TeamId = null,
    int? SourceVersion = null, Guid? MatchId = null);
public sealed record FormationSavedDto(bool IsOk, string Message, TeamRosterDto Team);
