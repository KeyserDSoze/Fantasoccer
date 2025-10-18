using System.Text.Json.Serialization;
using RepositoryFramework;

namespace Fantasoccer.Domain
{
    public sealed class AuctionBid
    {
        [JsonPropertyName("a")]
        public int Amount { get; set; }

        [JsonPropertyName("e")]
        public string BidderEmail { get; set; }

        [JsonPropertyName("n")]
        public string BidderName { get; set; }

        [JsonPropertyName("p")]
        public string PlayerName { get; set; }

        [JsonPropertyName("s")]
        public string Substituted { get; set; }
    }
    public sealed class PlayerAssignedInAuction
    {
        [JsonPropertyName("o")]
        public string Owner { get; set; }

        [JsonPropertyName("t")]
        public string TeamName { get; set; }

        [JsonPropertyName("p")]
        public int Price { get; set; }

        [JsonPropertyName("n")]
        public string PlayerName { get; set; }

        [JsonPropertyName("c")]
        public int TeamCost { get; set; }

        [JsonPropertyName("pc")]
        public int TeamPlayersCount { get; set; }
    }
    public sealed class PlayerAssignedMessageInAuction
    {
        [JsonPropertyName("p")]
        public PlayerAssignedInAuction? PlayerAssignedInAuction { get; set; }
        [JsonPropertyName("t")]
        public Entity<Team, TeamKey>? Team { get; set; }
    }
    public sealed class AuctionHubWrapper
    {
        [JsonPropertyName("w")]
        public required AuctionWrapper Wrapper { get; set; }
        [JsonPropertyName("u")]
        public DateTime LastUpdate { get; set; }
        [JsonPropertyName("g")]
        public Group? Group { get; set; }
        [JsonPropertyName("l")]
        public AnnualLeague? League { get; set; }
        [JsonPropertyName("ln")]
        public string? LeagueName { get; set; }
        [JsonPropertyName("t")]
        public List<Entity<Team, TeamKey>> Teams { get; set; } = [];
        [JsonPropertyName("r")]
        public List<StatPlayer>? RealPlayers { get; set; }
        [JsonPropertyName("rc")]
        public RealCalendar? RealCalendar { get; set; }
        [JsonIgnore]
        public string LastJsonSaved { get; set; }
    }
    public enum AuctionType
    {
        Normal,
        RandomByLetter,
        RandomList
    }
    public enum AuctionKind
    {
        Starting,
        Repairing
    }
    public enum AuctionStatus
    {
        NotStarted,
        Paused,
        InProgress,
        Finished
    }
    public sealed class PlayerForAuction
    {
        [JsonPropertyName("k")]
        public required string EncryptedKey { get; set; }
        [JsonPropertyName("i")]
        public required bool IsShowed { get; set; }
    }
    public sealed class AuctionWrapper
    {
        [JsonPropertyName("i")]
        public required string Id { get; set; }
        [JsonPropertyName("l")]
        public required LeagueKey LeagueKey { get; set; }
        [JsonPropertyName("c")]
        public required string Creator { get; set; }
        [JsonPropertyName("t")]
        public required DateTime Created { get; set; }
        [JsonPropertyName("tp")]
        public AuctionType Type { get; set; }
        [JsonPropertyName("knd")]
        public AuctionKind Kind { get; set; }
        [JsonPropertyName("a")]
        public ActualAuction? Actual { get; set; }
        [JsonPropertyName("w")]
        public List<AuctionWin> Winnings { get; set; } = [];
        [JsonPropertyName("s")]
        public AuctionStatus Status { get; set; }
        [JsonPropertyName("p")]
        public Dictionary<Role, List<PlayerForAuction>> ListOfPlayers { get; set; } = [];
        /// <summary>
        /// Here we put the connected participants use to inform who can bid
        /// </summary>
        [JsonPropertyName("pr")]
        public List<AnnualTeam> Participants { get; set; } = [];
        /// <summary>
        /// Last player shown to the participants
        /// </summary>
        [JsonPropertyName("ls")]
        public required Dictionary<Role, string?> LastShow { get; set; }
        [JsonPropertyName("sec")]
        public int SecondsPerAuction { get; set; } = 10;
    }
    public sealed class ActualAuction
    {
        [JsonPropertyName("pl")]
        public required StatPlayer Player { get; set; }
        [JsonPropertyName("s")]
        public required DateTime Started { get; set; }
        /// <summary>
        /// Here the email of the owner is used as identifier who gets the player
        /// </summary>
        [JsonPropertyName("o")]
        public string? Owner { get; set; }
        [JsonPropertyName("sb")]
        public string? Substituted { get; set; }
        [JsonPropertyName("p")]
        public int Price { get; set; }
        [JsonPropertyName("t")]
        public DateTime? StartingTime { get; set; }
    }
    public sealed class AuctionWin
    {
        [JsonPropertyName("k")]
        public required string PlayerKey { get; set; }
        /// <summary>
        /// Here the email of the owner is used as identifier who gets the player
        /// </summary>
        [JsonPropertyName("o")]
        public string? Owner { get; set; }
        [JsonPropertyName("p")]
        public int Price { get; set; }

    }
}
