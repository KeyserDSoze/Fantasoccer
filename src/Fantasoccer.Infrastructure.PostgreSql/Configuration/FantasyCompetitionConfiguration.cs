using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class FantasyCalendarConfiguration : IEntityTypeConfiguration<FantasyCalendar>
{
    public void Configure(EntityTypeBuilder<FantasyCalendar> builder)
    {
        builder.ToTable("FantasyCalendars");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
        builder.HasOne<LeagueSeason>().WithOne().HasForeignKey<FantasyCalendar>(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasMany(x => x.Stages).WithOne().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Cascade);
    }
}
internal sealed class FantasyStageConfiguration : IEntityTypeConfiguration<FantasyStage>
{
    public void Configure(EntityTypeBuilder<FantasyStage> builder)
    {
        builder.ToTable("FantasyStages");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Stage });
        builder.HasMany(x => x.Days).WithOne().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Stage }).OnDelete(DeleteBehavior.Cascade);
    }
}
internal sealed class FantasyMatchdayConfiguration : IEntityTypeConfiguration<FantasyMatchday>
{
    public void Configure(EntityTypeBuilder<FantasyMatchday> builder)
    {
        builder.ToTable("FantasyMatchdays", table => table.HasCheckConstraint("CK_FantasyMatchdays_Day", "\"SerieADay\" BETWEEN 1 AND 38"));
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Stage, x.Ordinal });
        builder.Property(x => x.Ordinal).ValueGeneratedNever();
        builder.HasMany(x => x.Games).WithOne(x => x.Matchday).HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Stage, x.DayOrdinal }).OnDelete(DeleteBehavior.Cascade);
    }
}
internal sealed class FantasyMatchConfiguration : IEntityTypeConfiguration<FantasyMatch>
{
    public void Configure(EntityTypeBuilder<FantasyMatch> builder)
    {
        builder.ToTable("FantasyMatches", table =>
        {
            table.HasCheckConstraint("CK_FantasyMatches_Home", "(\"HomeBasketId\" IS NULL) = (\"HomeTeamId\" IS NULL)");
            table.HasCheckConstraint("CK_FantasyMatches_Away", "(\"AwayBasketId\" IS NULL) = (\"AwayTeamId\" IS NULL)");
        });
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Id });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.HomeBasketId, x.SeasonIndex, x.HomeTeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.AwayBasketId, x.SeasonIndex, x.AwayTeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeagueBasketSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.HomeBasketId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeagueBasketSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.AwayBasketId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(x => x.Result, Result);
    }
    internal static void Result<T>(OwnedNavigationBuilder<T, SavedFantasyResult> result) where T : class
    {
        result.OwnsOne(x => x.Home);
        result.OwnsOne(x => x.Away);
    }
}
internal sealed class FantasyStandingConfiguration : IEntityTypeConfiguration<FantasyStandingSnapshot>
{
    public void Configure(EntityTypeBuilder<FantasyStandingSnapshot> builder)
    {
        builder.ToTable("FantasyStandingSnapshots", table => table.HasCheckConstraint("CK_FantasyStandingSnapshots_Day", "\"SnapshotDay\" BETWEEN 0 AND 38 AND \"SerieADay\" BETWEEN 0 AND 38"));
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SnapshotDay });
        builder.HasOne<LeagueSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.Stages, stage =>
        {
            stage.ToTable("FantasyStandingStages");
            stage.WithOwner().HasForeignKey("GroupId", "LeagueId", "SeasonIndex", "SnapshotDay");
            stage.HasKey("GroupId", "LeagueId", "SeasonIndex", "SnapshotDay", "Stage");
            stage.OwnsMany(x => x.Teams, row =>
            {
                row.ToTable("FantasyStandingRows");
                row.WithOwner().HasForeignKey("GroupId", "LeagueId", "SeasonIndex", "SnapshotDay", "Stage");
                row.HasKey("GroupId", "LeagueId", "SeasonIndex", "SnapshotDay", "Stage", "Ordinal");
                row.Property(x => x.Ordinal).ValueGeneratedNever();
                row.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey("GroupId", "BasketId", "SeasonIndex", "TeamId")
                    .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
                row.HasOne<LeagueBasketSeason>().WithMany().HasForeignKey("GroupId", "LeagueId", "BasketId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            });
        });
    }
}
internal sealed class PlayedMatchConfiguration : IEntityTypeConfiguration<PlayedMatchSnapshot>
{
    public void Configure(EntityTypeBuilder<PlayedMatchSnapshot> builder)
    {
        builder.ToTable("PlayedMatchSnapshots");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.MatchId, x.Day });
        builder.HasOne<FantasyMatch>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.MatchId }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(x => x.Result, FantasyMatchConfiguration.Result);
        builder.OwnsMany(x => x.Players, row =>
        {
            row.ToTable("PlayedMatchPlayers");
            row.WithOwner().HasForeignKey("GroupId", "LeagueId", "SeasonIndex", "MatchId", "Day");
            row.HasKey("GroupId", "LeagueId", "SeasonIndex", "MatchId", "Day", "IsHome", "Ordinal");
            row.Property(x => x.Ordinal).ValueGeneratedNever();
            row.HasOne<PlayerSeason>().WithMany().HasForeignKey("PlayerId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            row.HasOne<FootballClub>().WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
