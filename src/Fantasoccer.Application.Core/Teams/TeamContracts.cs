namespace Fantasoccer.Application.Core.Teams;

public sealed record FootballClubDto(string Name, string Abbreviation);
public sealed record RosterPlayerDto(Guid PlayerId, int Ordinal, string Name, FootballClubDto Team, int Role, bool IsActive,
    bool Visible, int Price, int Revenue, int Status, int Position);
public sealed record FormationPlayerChangeDto(string Name, int FromPosition, int ToPosition);
public sealed record FormationChangeDto(string? Date, string Author, FormationPlayerChangeDto[] Players);
public sealed record TeamRosterDto(Guid TeamId, Guid GroupId, Guid BasketId, int SeasonIndex, int Kind, int Day,
    Guid? LeagueId, string Name, string Owner, string[] AdditionalOwners, RosterPlayerDto[] Players,
    int MoneyFromRank, int OpeningCompetitionPrize, string? LastUpdate, int FormationChanges, FormationChangeDto[] FormationChangeHistory, int Version, string? LogoUrl = null, int? SourceVersion = null,
    int MarketBalanceAdjustment = 0, int? RankCreditGranted = null);
public sealed record FormationPositionRequest(Guid PlayerId, int Position);
public sealed record SaveRosterFormationRequest(Guid GroupId, Guid BasketId, int Year, string Owner, int Day,
    Guid? LeagueId, int Kind, FormationPositionRequest[] Positions, int ExpectedVersion, bool Live = false, bool AsAdmin = false, Guid? TeamId = null,
    int? SourceVersion = null, Guid? MatchId = null);
public sealed record FormationSavedDto(bool IsOk, string Message, TeamRosterDto Team);
