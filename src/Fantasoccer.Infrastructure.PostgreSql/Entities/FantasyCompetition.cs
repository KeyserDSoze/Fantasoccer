namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class FantasyCalendar
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public int Version { get; set; } = 1;
    public ICollection<FantasyStage> Stages { get; set; } = [];
}
public sealed class FantasyStage
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public required string Stage { get; set; }
    public int Ordinal { get; set; }
    public ICollection<FantasyMatchday> Days { get; set; } = [];
}
public sealed class FantasyMatchday
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public required string Stage { get; set; }
    public int Ordinal { get; set; }
    public int FantasyTurn { get; set; }
    public int SerieADay { get; set; }
    public ICollection<FantasyMatch> Games { get; set; } = [];
}
public sealed class FantasyMatch
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid Id { get; set; }
    public required string Stage { get; set; }
    public int DayOrdinal { get; set; }
    public int Ordinal { get; set; }
    public int Number { get; set; }
    public Guid? HomeBasketId { get; set; }
    public Guid? HomeTeamId { get; set; }
    public Guid? AwayBasketId { get; set; }
    public Guid? AwayTeamId { get; set; }
    public required string HomeName { get; set; }
    public required string AwayName { get; set; }
    public string? HomeOwner { get; set; }
    public string? AwayOwner { get; set; }
    public SavedFantasyResult? Result { get; set; }
    public FantasyMatchday Matchday { get; set; } = null!;
}
public sealed class SavedFantasyResult
{
    public bool IsCancelled { get; set; }
    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public SavedFantasyPoint? Home { get; set; }
    public SavedFantasyPoint? Away { get; set; }
}
public sealed class SavedFantasyPoint
{
    public float Value { get; set; }
    public bool DefensiveBonus { get; set; }
    public bool GoodPeople { get; set; }
    public bool OwnGoal { get; set; }
}
public sealed class FantasyStandingSnapshot
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public int SnapshotDay { get; set; }
    public int SerieADay { get; set; }
    public ICollection<FantasyStandingStage> Stages { get; set; } = [];
}
public sealed class FantasyStandingStage
{
    public required string Stage { get; set; }
    public int Ordinal { get; set; }
    public ICollection<FantasyStandingRow> Teams { get; set; } = [];
}
public sealed class FantasyStandingRow
{
    public int Ordinal { get; set; }
    public Guid BasketId { get; set; }
    public Guid TeamId { get; set; }
    public required string Name { get; set; }
    public required string Owner { get; set; }
    public float Points { get; set; }
    public int Victories { get; set; }
    public int Draws { get; set; }
    public int Defeats { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public float ValuePoints { get; set; }
    public float SufferedValuePoints { get; set; }
    public int PlusMoney { get; set; }
    public int Money { get; set; }
}
public sealed class PlayedMatchSnapshot
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid MatchId { get; set; }
    public int Day { get; set; }
    public required string HomeName { get; set; }
    public required string AwayName { get; set; }
    public SavedFantasyResult? Result { get; set; }
    public ICollection<PlayedMatchPlayer> Players { get; set; } = [];
}
public sealed class PlayedMatchPlayer
{
    public bool IsHome { get; set; }
    public int Ordinal { get; set; }
    public string PlayerId { get; set; } = null!;
    public string ClubId { get; set; } = null!;
    public int PlayerRole { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int Price { get; set; }
    public int Revenue { get; set; }
    public int Status { get; set; }
    public int Position { get; set; }
}
