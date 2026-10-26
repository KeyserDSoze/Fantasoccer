namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class HallArchive
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int Version { get; set; } = 1;
    public bool ContributionsInitialized { get; set; }
    public HallRecordGame? RecordGame { get; set; }
    public ICollection<HallPlayerRecord> PlayerRecords { get; set; } = [];
    public ICollection<HallWinningPlayer> WinningPlayers { get; set; } = [];
    public ICollection<HallWinningOwner> WinningTeams { get; set; } = [];
    public ICollection<HallCareerRank> Ranks { get; set; } = [];
}
public sealed class HallRecordGame
{
    public int Year { get; set; }
    public Guid Id { get; set; }
    public Guid? LinkedMatchId { get; set; }
    public required string LegacyGameId { get; set; }
    public int Number { get; set; }
    public required string Home { get; set; }
    public string? HomeOwner { get; set; }
    public required string Away { get; set; }
    public string? AwayOwner { get; set; }
    public SavedFantasyResult? Result { get; set; }
}
public sealed class HallPlayerSnapshot
{
    public Guid PlayerId { get; set; }
    public Guid ClubId { get; set; }
    public required string Name { get; set; }
    public int PlayerRole { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int Price { get; set; }
    public int Revenue { get; set; }
    public int Status { get; set; }
    public int Position { get; set; }
    public FootballClub Club { get; set; } = null!;
}
public sealed class HallPlayerRecord
{
    public int Kind { get; set; } // 0 = highest single score, 1 = season total
    public int? Year { get; set; }
    public float Points { get; set; }
    public HallPlayerSnapshot Player { get; set; } = null!;
}
public sealed class HallWinningPlayer
{
    public int Ordinal { get; set; }
    public HallPlayerSnapshot Player { get; set; } = null!;
    public ICollection<HallWinStage> Wins { get; set; } = [];
}
public sealed class HallWinningOwner
{
    public int Ordinal { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string OwnerEmail { get; set; }
    public string? Name { get; set; }
    public ICollection<HallWinStage> Wins { get; set; } = [];
}
public sealed class HallWinStage
{
    public required string Stage { get; set; }
    public int Ordinal { get; set; }
    public ICollection<HallWinYear> Years { get; set; } = [];
}
public sealed class HallWinYear
{
    public int Ordinal { get; set; }
    public int Year { get; set; }
}
// The original career rank was aggregated by owner across years, not by a
// single annual team. Keep the owner identity rather than inventing a season FK.
public sealed class HallCareerRank
{
    public int Ordinal { get; set; }
    public Guid OwnerUserId { get; set; }
    public required string OwnerEmail { get; set; }
    public required string Name { get; set; }
    public float Points { get; set; }
    public int Victories { get; set; }
    public int Draws { get; set; }
    public int Defeats { get; set; }
    public int Goals { get; set; }
    public int SufferedGoals { get; set; }
    public float ValuePoints { get; set; }
    public float SufferedValuePoints { get; set; }
    public int PlusMoney { get; set; }
    public int Money { get; set; }
}
