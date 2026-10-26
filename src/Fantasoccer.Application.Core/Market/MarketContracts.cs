using Fantasoccer.Application.Core.Teams;

namespace Fantasoccer.Application.Core.Market;

public sealed record MarketDto(Guid Id, Guid BuyerTeamId, Guid SellerTeamId, string Buyer, string Seller, RosterPlayerDto[] BuyerPlayers,
    RosterPlayerDto[] SellerPlayers, int MoneyFromBuyer, int MoneyFromSeller, string[] Approvers, string[] Deniers,
    bool SellerAccepted, int Status, DateTimeOffset CreationTime, int Version);
public sealed record CreateMarketRequest(Guid BuyerTeamId, Guid SellerTeamId, string[] BuyerPlayerIds, string[] SellerPlayerIds,
    int MoneyFromBuyer, int MoneyFromSeller, int ExpectedBuyerRosterVersion, int ExpectedSellerRosterVersion);
public sealed record MarketCommandRequest(int ExpectedVersion, Guid? ActorTeamId = null);
public sealed record MarketOperationDto(bool IsOk, string Message, MarketDto Market);
