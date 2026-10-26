namespace Fantasoccer.Infrastructure.PostgreSql.Entities;

public sealed class MarketBook
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
}
public sealed class MarketTrade
{
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid Id { get; set; }
    public int Ordinal { get; set; }
    public Guid BuyerTeamId { get; set; }
    public Guid BuyerBasketId { get; set; }
    public Guid SellerTeamId { get; set; }
    public Guid SellerBasketId { get; set; }
    public required string BuyerOwner { get; set; }
    public required string SellerOwner { get; set; }
    public int MoneyFromBuyer { get; set; }
    public int MoneyFromSeller { get; set; }
    public bool SellerAccepted { get; set; }
    public int Status { get; set; }
    public DateTime? LegacyCreatedUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? ExecutedAtUtc { get; set; }
    public int Version { get; set; } = 1;
    public ICollection<MarketTradePlayer> Players { get; set; } = [];
    public ICollection<MarketTradeVote> Votes { get; set; } = [];
}
public sealed class MarketTradePlayer
{
    public bool FromBuyer { get; set; }
    public int Ordinal { get; set; }
    public int? RosterOrdinal { get; set; }
    public string PlayerId { get; set; } = null!;
    public string ClubId { get; set; } = null!;
    public int PlayerRole { get; set; }
    public bool IsActive { get; set; }
    public bool IsVisible { get; set; }
    public int Price { get; set; }
    public int Revenue { get; set; }
    public int Status { get; set; }
    public int Position { get; set; }
    public FootballPlayer Player { get; set; } = null!;
    public FootballClub Club { get; set; } = null!;
}
public sealed class MarketTradeVote
{
    public Guid TeamId { get; set; }
    public Guid BasketId { get; set; }
    public Guid? ActorUserId { get; set; }
    public required string OwnerEmail { get; set; }
    public bool Approved { get; set; }
    public int Ordinal { get; set; }
}

// Durable typed events, committed with the trade/rosters. Notification delivery
// can be retried without recreating a trade or sending from an import/GET.
public sealed class MarketTradeEvent
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid LeagueId { get; set; }
    public int SeasonIndex { get; set; }
    public Guid TradeId { get; set; }
    public Guid? ActorUserId { get; set; }
    public required string Kind { get; set; }
    public int ResultingStatus { get; set; }
    public int TradeVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? DeliveredAtUtc { get; set; }
    public DateTimeOffset? QueuedAtUtc { get; set; }
}
