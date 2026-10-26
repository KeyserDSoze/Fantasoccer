using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class HallArchiveConfiguration : IEntityTypeConfiguration<HallArchive>
{
    public void Configure(EntityTypeBuilder<HallArchive> builder)
    {
        builder.ToTable("HallArchives");
        builder.HasKey(x => new { x.GroupId, x.LeagueId });
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<GroupLeague>().WithOne().HasForeignKey<HallArchive>(x => new { x.GroupId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(x => x.RecordGame, game =>
        {
            game.ToTable("HallRecordGames");
            game.HasOne<Season>().WithMany().HasForeignKey(x => x.Year).OnDelete(DeleteBehavior.Restrict);
            game.HasOne<FantasyMatch>().WithMany().HasForeignKey("GroupId", "LeagueId", "Year", "LinkedMatchId").OnDelete(DeleteBehavior.Restrict);
            game.OwnsOne(x => x.Result, FantasyMatchConfiguration.Result);
        });
        builder.OwnsMany(x => x.PlayerRecords, row =>
        {
            row.ToTable("HallPlayerRecords"); row.WithOwner().HasForeignKey("GroupId", "LeagueId");
            row.HasKey("GroupId", "LeagueId", "Kind"); row.Property(x => x.Kind).ValueGeneratedNever();
            row.HasOne<Season>().WithMany().HasForeignKey(x => x.Year).OnDelete(DeleteBehavior.Restrict);
            row.OwnsOne(x => x.Player, Snapshot);
        });
        builder.OwnsMany(x => x.WinningPlayers, row =>
        {
            row.ToTable("HallWinningPlayers"); row.WithOwner().HasForeignKey("GroupId", "LeagueId");
            row.HasKey("GroupId", "LeagueId", "Ordinal"); row.Property(x => x.Ordinal).ValueGeneratedNever();
            row.OwnsOne(x => x.Player, Snapshot);
            row.OwnsMany(x => x.Wins, stage => Stages(stage, "HallPlayerWinStages", "HallPlayerWinYears"));
        });
        builder.OwnsMany(x => x.WinningTeams, row =>
        {
            row.ToTable("HallWinningOwners"); row.WithOwner().HasForeignKey("GroupId", "LeagueId");
            row.HasKey("GroupId", "LeagueId", "Ordinal"); row.Property(x => x.Ordinal).ValueGeneratedNever();
            row.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            row.OwnsMany(x => x.Wins, stage => Stages(stage, "HallOwnerWinStages", "HallOwnerWinYears"));
        });
        builder.OwnsMany(x => x.Ranks, row =>
        {
            row.ToTable("HallCareerRanks"); row.WithOwner().HasForeignKey("GroupId", "LeagueId");
            row.HasKey("GroupId", "LeagueId", "Ordinal"); row.Property(x => x.Ordinal).ValueGeneratedNever();
            row.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
    private static void Snapshot<T>(OwnedNavigationBuilder<T, HallPlayerSnapshot> player) where T : class
    {
        player.HasOne<FootballPlayer>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
        player.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
    }
    private static void Stages<T>(OwnedNavigationBuilder<T, HallWinStage> stage, string table, string years) where T : class
    {
        stage.ToTable(table); stage.WithOwner().HasForeignKey("GroupId", "LeagueId", "WinnerOrdinal");
        stage.HasKey("GroupId", "LeagueId", "WinnerOrdinal", "Stage");
        stage.OwnsMany(x => x.Years, year =>
        {
            year.ToTable(years); year.WithOwner().HasForeignKey("GroupId", "LeagueId", "WinnerOrdinal", "Stage");
            year.HasKey("GroupId", "LeagueId", "WinnerOrdinal", "Stage", "Ordinal"); year.Property(x => x.Ordinal).ValueGeneratedNever();
            year.HasOne<Season>().WithMany().HasForeignKey(x => x.Year).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
