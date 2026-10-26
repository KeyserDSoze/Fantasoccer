using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class MarketBookConfiguration : IEntityTypeConfiguration<MarketBook>
{
    public void Configure(EntityTypeBuilder<MarketBook> builder)
    {
        builder.ToTable("MarketBooks");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
        builder.HasOne<LeagueSeason>().WithOne().HasForeignKey<MarketBook>(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class MarketTradeConfiguration : IEntityTypeConfiguration<MarketTrade>
{
    public void Configure(EntityTypeBuilder<MarketTrade> builder)
    {
        builder.ToTable("MarketTrades", table =>
        {
            table.HasCheckConstraint("CK_MarketTrades_DistinctTeams", "\"BuyerTeamId\" <> \"SellerTeamId\"");
            table.HasCheckConstraint("CK_MarketTrades_Status", "\"Status\" BETWEEN 0 AND 6");
        });
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Id });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.LegacyCreatedUtc).HasColumnType("timestamp without time zone");
        builder.HasOne<MarketBook>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.BuyerBasketId, x.SeasonIndex, x.BuyerTeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.SellerBasketId, x.SeasonIndex, x.SellerTeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeagueBasketSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.BuyerBasketId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeagueBasketSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SellerBasketId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.Players, row =>
        {
            row.ToTable("MarketTradePlayers");
            row.WithOwner().HasForeignKey("GroupId", "LeagueId", "SeasonIndex", "TradeId");
            row.HasKey("GroupId", "LeagueId", "SeasonIndex", "TradeId", "FromBuyer", "Ordinal");
            row.Property(x => x.Ordinal).ValueGeneratedNever();
            row.HasOne<PlayerSeason>().WithMany().HasForeignKey("PlayerId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            row.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
            row.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.OwnsMany(x => x.Votes, row =>
        {
            row.ToTable("MarketTradeVotes");
            row.WithOwner().HasForeignKey("GroupId", "LeagueId", "SeasonIndex", "TradeId");
            row.HasKey("GroupId", "LeagueId", "SeasonIndex", "TradeId", "TeamId");
            row.Property(x => x.TeamId).ValueGeneratedNever();
            row.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey("GroupId", "BasketId", "SeasonIndex", "TeamId")
                .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
            row.HasOne<LeagueBasketSeason>().WithMany().HasForeignKey("GroupId", "LeagueId", "BasketId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            row.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class MarketTradeEventConfiguration : IEntityTypeConfiguration<MarketTradeEvent>
{
    public void Configure(EntityTypeBuilder<MarketTradeEvent> builder)
    {
        builder.ToTable("MarketTradeEvents");
        builder.HasKey(x => x.Id);
        builder.HasOne<MarketTrade>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TradeId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TradeId, x.TradeVersion }).IsUnique();
        builder.HasIndex(x => x.CreatedAtUtc).HasFilter("\"DeliveredAtUtc\" IS NULL");
    }
}
