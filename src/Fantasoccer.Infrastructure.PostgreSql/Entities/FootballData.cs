namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class FootballDataset
{
    public required string Kind { get; set; }
    public int SeasonIndex { get; set; }
    public int Day { get; set; }
}

public enum FootballVoteSource { Live = 0, Notification = 1, Official = 2 }
public sealed class FootballVote
{
    public string PlayerId { get; set; } = null!;
    public int SeasonIndex { get; set; }
    public int Day { get; set; }
    public FootballVoteSource Source { get; set; }
    public string ClubId { get; set; } = null!;
    public int PlayerRole { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int VoteRole { get; set; }
    public float Value { get; set; }
    public bool IsFinal { get; set; }
    public int Goals { get; set; }
    public int Penalties { get; set; }
    public int Assists { get; set; }
    public int StoppedPenalties { get; set; }
    public int SufferedGoals { get; set; }
    public int WrongedPenalties { get; set; }
    public int OwnGoals { get; set; }
    public int Status { get; set; }
    public bool ManOfTheMatch { get; set; }
    public bool HasVote { get; set; }
    public bool IsOut { get; set; }
    public bool IsIn { get; set; }
    public bool Injured { get; set; }
    public FootballPlayer Player { get; set; } = null!;
    public FootballClub Club { get; set; } = null!;
}

public sealed class FootballChance
{
    public string PlayerId { get; set; } = null!;
    public int SeasonIndex { get; set; }
    public int Day { get; set; }
    public string ClubId { get; set; } = null!;
    public int PlayerRole { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public bool Fantagazzetta { get; set; }
    public bool Gazzetta { get; set; }
    public bool Mediaset { get; set; }
    public bool Sky { get; set; }
    public int Status { get; set; }
    public string? Description { get; set; }
    public int? LegacyLastGameDay { get; set; }
    public float? LegacyLastGameVote { get; set; }
    public int? LegacyLastGamePositiveness { get; set; }
    public int? LegacyTrend { get; set; }
    public FootballPlayer Player { get; set; } = null!;
    public FootballClub Club { get; set; } = null!;
}

public sealed class FootballPlayerStatistic
{
    public string PlayerId { get; set; } = null!;
    public int SeasonIndex { get; set; }
    public string ClubId { get; set; } = null!;
    public int PlayerRole { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public float Summatory { get; set; }
    public float FantaSummatory { get; set; }
    public int WithVote { get; set; }
    public int WithoutVote { get; set; }
    public int NoPlayed { get; set; }
    public int WithSpecial { get; set; }
    public int Goals { get; set; }
    public int Penalties { get; set; }
    public int Assists { get; set; }
    public int StoppedPenalties { get; set; }
    public int SufferedGoals { get; set; }
    public int WrongedPenalties { get; set; }
    public int OwnGoals { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int EnoughVotes { get; set; }
    public int ManOfTheMatch { get; set; }
    public int Injured { get; set; }
    public string? DescriptionTop { get; set; }
    public string? DescriptionDown { get; set; }
    public bool HasDescription { get; set; }
    public bool HasGameHistory { get; set; }
    public ICollection<FootballStatisticGame> Games { get; set; } = [];
    public FootballPlayer Player { get; set; } = null!;
    public FootballClub Club { get; set; } = null!;
}
public sealed class FootballStatisticGame
{
    public int Day { get; set; }
    public int Ordinal { get; set; }
    public float? Vote { get; set; }
    public int Positiveness { get; set; }
}

public sealed class FootballClubRank
{
    public int SeasonIndex { get; set; }
    public string ClubId { get; set; } = null!;
    public int Ordinal { get; set; }
    public float Points { get; set; }
    public int Victories { get; set; }
    public int Draws { get; set; }
    public int Defeats { get; set; }
    public int GoalsFor { get; set; }
    public int GoalsAgainst { get; set; }
    public int Played { get; set; }
    public FootballClub Club { get; set; } = null!;
}
