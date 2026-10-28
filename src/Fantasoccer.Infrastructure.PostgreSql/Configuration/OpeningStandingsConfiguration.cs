using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class OpeningStandingsConfiguration : IEntityTypeConfiguration<OpeningStandingSnapshot>
{
    public void Configure(EntityTypeBuilder<OpeningStandingSnapshot> builder)
    {
        builder.ToTable("OpeningStandingSnapshots");
        builder.Property(x => x.IsComplete).HasDefaultValue(true).ValueGeneratedNever();
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<LeagueSeason>().WithOne().HasForeignKey<OpeningStandingSnapshot>(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.Teams, row =>
        {
            row.ToTable("OpeningStandingRows", table => table.HasCheckConstraint("CK_OpeningStandingRows_Origin", "\"TeamOriginYear\" = \"SeasonIndex\" - 1"));
            row.WithOwner().HasForeignKey("GroupId", "LeagueId", "SeasonIndex");
            row.HasKey("GroupId", "LeagueId", "SeasonIndex", "TeamId");
            row.Property(x => x.TeamId).ValueGeneratedNever();
            row.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey("GroupId", "BasketId", "TeamOriginYear", "TeamId")
                .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
            row.HasOne<GroupLeagueBasket>().WithMany().HasForeignKey("GroupId", "LeagueId", "BasketId").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
