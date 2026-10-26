namespace Fantasoccer.Application.Core.Football;

public sealed record ClubDto(string ClubId, string Name, string Abbreviation)
{
    public string ClubCode => ClubId;
}
public record FootballPlayerDto(string PlayerId, string Name, ClubDto? RealTeam, int Role, bool IsActive, bool IsVisible)
{
    // Compatibility ID/alias names carry the same natural player key.
    public string Key => PlayerId;
    public int? SeasonIndex { get; init; }
    public string PlayerKey => PlayerId;
    public string RoleLabel => ((Role)Role).AsString();
}
public sealed record PlayersDto<T>(T[] Players);
public sealed record PlayerHistoryDto(string PlayerKey, string Name, int[] Years);
public sealed record VoteDto(int Role, float Value, bool IsFinal, int Goals, int Penalties, int Assists, int StoppedPenalties,
    int SufferedGoals, int WrongedPenalties, int OwnGoals, int Status, bool ManOfTheMatch, bool HasVote, bool IsOut, bool IsIn, bool Injured)
{
    public bool HasPositiveBonus => Goals > 0 || Penalties > 0 || Assists > 0 || StoppedPenalties > 0;
    public bool HasNegativeBonus => OwnGoals > 0 || Status > 0 || WrongedPenalties > 0 || SufferedGoals > 0;
    public bool HasDoneSomething => HasPositiveBonus || HasNegativeBonus || Value > 0 && Role == 0;
}
public sealed record VotedPlayerDto(string PlayerId, string Name, ClubDto? RealTeam, int Role, bool IsActive, bool IsVisible, VoteDto Vote)
    : FootballPlayerDto(PlayerId, Name, RealTeam, Role, IsActive, IsVisible);
public sealed record ChanceDto(bool Fantagazzetta, bool Gazzetta, bool Mediaset, bool Sky, int Status, string? Description,
    StatisticGameDto? LastGame = null, int? Trend = null);
public sealed record ChancedPlayerDto(string PlayerId, string Name, ClubDto? RealTeam, int Role, bool IsActive, bool IsVisible, ChanceDto Chance)
    : FootballPlayerDto(PlayerId, Name, RealTeam, Role, IsActive, IsVisible);
public sealed record StatisticGameDto(int SerieADay, float? Vote, int Positiveness);
public sealed record DescriptionDto(string? Top, string? Down);
public sealed record StatisticPlayerDto(string PlayerId, string Name, ClubDto? RealTeam, int Role, bool IsActive, bool IsVisible,
    float Summatory, float FantaSummatory, int WithVote, int WithoutVote, int NoPlayed, int WithSpecial, int Goals, int Penalties,
    int Assists, int StoppedPenalties, int SufferedGoals, int WrongedPenalties, int OwnGoals, int YellowCards, int RedCards,
    int EnoughVotes, int ManOfTheMatch, int Injured, StatisticGameDto[] Games, DescriptionDto? Description)
    : FootballPlayerDto(PlayerId, Name, RealTeam, Role, IsActive, IsVisible)
{
    public float Average => WithVote > 0 ? Summatory / WithVote : 0;
    public float FantaAverage => WithVote > 0 ? FantaSummatory / WithVote : 0;
}
public sealed record FootballGameDto(int Ordinal, ClubDto Home, ClubDto Away, DateTimeOffset? Date, int? HomeGoals, int? AwayGoals, bool Delayed);
public sealed record FootballDayDto(int Year, int SerieADay, FootballGameDto[] Games);
public sealed record FootballCalendarDto(int Year, FootballDayDto[] Days);
public sealed record ClubRankDto(string ClubId, string Name, float Points, int Victories, int Draws, int Defeats, int GoalsFor, int GoalsAgainst, int Played)
{
    public int GoalDifference => GoalsFor - GoalsAgainst;
}
public sealed record ClubStandingsDto(ClubRankDto[] Teams);
public sealed record DelayFootballGameRequest(bool Delayed, bool ExpectedDelayed);
