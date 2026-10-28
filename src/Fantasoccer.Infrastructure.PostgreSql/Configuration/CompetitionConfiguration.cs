using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class GroupBasketConfiguration : IEntityTypeConfiguration<GroupBasket>
{
    public void Configure(EntityTypeBuilder<GroupBasket> builder)
    {
        builder.ToTable("Baskets", table => table.HasCheckConstraint("CK_Baskets_Kind", "\"Kind\" IN (0,1)"));
        builder.HasKey(x => new { x.GroupId, x.BasketId });
        // Target of the derivation FKs; also makes the kind immutable once referenced.
        builder.HasAlternateKey(x => new { x.GroupId, x.BasketId, x.Kind });
        builder.Property(x => x.Kind).HasDefaultValue(BasketKind.Autonomous).ValueGeneratedNever();
        builder.Property(x => x.BasketId).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Group).WithMany(x => x.Baskets).HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BasketSeasonConfiguration : IEntityTypeConfiguration<BasketSeason>
{
    public void Configure(EntityTypeBuilder<BasketSeason> builder)
    {
        builder.ToTable("BasketSeasons");
        builder.HasKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex });
        builder.HasOne(x => x.Basket).WithMany(x => x.Seasons).HasForeignKey(x => new { x.GroupId, x.BasketId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Season).WithMany().HasForeignKey(x => x.SeasonIndex)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GroupLeagueConfiguration : IEntityTypeConfiguration<GroupLeague>
{
    public void Configure(EntityTypeBuilder<GroupLeague> builder)
    {
        builder.ToTable("Leagues", table => table.HasCheckConstraint("CK_Leagues_Mode", "\"Mode\" IN (0,1)"));
        builder.HasKey(x => new { x.GroupId, x.LeagueId });
        builder.Property(x => x.Mode).HasDefaultValue(LeagueMode.Autonomous).ValueGeneratedNever();
        builder.Property(x => x.LeagueId).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasOne(x => x.Group).WithMany(x => x.Leagues).HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LeagueSeasonConfiguration : IEntityTypeConfiguration<LeagueSeason>
{
    public void Configure(EntityTypeBuilder<LeagueSeason> builder)
    {
        builder.ToTable("LeagueSeasons");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
        builder.HasOne(x => x.League).WithMany(x => x.Seasons).HasForeignKey(x => new { x.GroupId, x.LeagueId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Season).WithMany().HasForeignKey(x => x.SeasonIndex)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LeagueBasketSeasonConfiguration : IEntityTypeConfiguration<LeagueBasketSeason>
{
    public void Configure(EntityTypeBuilder<LeagueBasketSeason> builder)
    {
        builder.ToTable("LeagueBasketSeasons");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.BasketId, x.SeasonIndex });
        builder.HasOne(x => x.LeagueSeason).WithMany(x => x.Baskets)
            .HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.BasketSeason).WithMany(x => x.Competitions)
            .HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class GroupLeagueBasketConfiguration : IEntityTypeConfiguration<GroupLeagueBasket>
{
    public void Configure(EntityTypeBuilder<GroupLeagueBasket> builder)
    {
        builder.ToTable("GroupLeagueBaskets");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.BasketId });
        builder.HasOne<GroupLeague>().WithMany(x => x.BasketLinks).HasForeignKey(x => new { x.GroupId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupBasket>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId }).OnDelete(DeleteBehavior.Restrict);
    }
}
