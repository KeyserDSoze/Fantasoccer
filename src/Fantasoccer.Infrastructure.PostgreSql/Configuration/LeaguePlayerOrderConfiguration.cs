using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class LeaguePlayerOrderConfiguration : IEntityTypeConfiguration<LeaguePlayerOrder>
{
    public void Configure(EntityTypeBuilder<LeaguePlayerOrder> builder)
    {
        builder.ToTable("LeaguePlayerOrders");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<LeagueSeason>().WithOne().HasForeignKey<LeaguePlayerOrder>(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.Players, row =>
        {
            row.ToTable("LeaguePlayerOrderEntries");
            row.WithOwner().HasForeignKey("GroupId", "LeagueId", "SeasonIndex");
            row.HasKey("GroupId", "LeagueId", "SeasonIndex", "Ordinal");
            row.Property(x => x.Ordinal).ValueGeneratedNever();
            row.HasIndex("GroupId", "LeagueId", "SeasonIndex", "PlayerId").IsUnique();
            row.HasOne<PlayerSeason>().WithMany().HasForeignKey("PlayerId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            row.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
            row.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
