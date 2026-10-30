using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class FootballDatasetConfiguration : IEntityTypeConfiguration<FootballDataset>
{
    public void Configure(EntityTypeBuilder<FootballDataset> builder)
    {
        builder.ToTable("FootballDatasets", table => table.HasCheckConstraint("CK_FootballDatasets_Day", "\"Day\" BETWEEN 0 AND 38"));
        builder.HasKey(x => new { x.Kind, x.SeasonIndex, x.Day });
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class FootballDayRevisionConfiguration : IEntityTypeConfiguration<FootballDayRevision>
{
    public void Configure(EntityTypeBuilder<FootballDayRevision> builder)
    {
        builder.ToTable("FootballDayRevisions", table => table.HasCheckConstraint("CK_FootballDayRevisions_Day", "\"Day\" BETWEEN 1 AND 38"));
        builder.HasKey(x => new { x.SeasonIndex, x.Day });
        builder.Property(x => x.OfficialRevision).HasDefaultValue(0L);
        // Derived data: it follows its season instead of blocking its removal.
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Cascade);
    }
}
internal sealed class FootballVoteConfiguration : IEntityTypeConfiguration<FootballVote>
{
    public void Configure(EntityTypeBuilder<FootballVote> builder)
    {
        builder.ToTable("FootballVotes", table =>
        {
            table.HasCheckConstraint("CK_FootballVotes_Day", "\"Day\" BETWEEN 1 AND 38");
            table.HasCheckConstraint("CK_FootballVotes_Source", "\"Source\" BETWEEN 0 AND 2");
        });
        builder.HasKey(x => new { x.PlayerId, x.SeasonIndex, x.Day, x.Source });
        builder.HasOne<PlayerSeason>().WithMany().HasForeignKey(x => new { x.PlayerId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SeasonIndex, x.Day, x.Source });
    }
}
internal sealed class FootballChanceConfiguration : IEntityTypeConfiguration<FootballChance>
{
    public void Configure(EntityTypeBuilder<FootballChance> builder)
    {
        builder.ToTable("FootballChances", table => table.HasCheckConstraint("CK_FootballChances_Day", "\"Day\" BETWEEN 1 AND 38"));
        builder.HasKey(x => new { x.PlayerId, x.SeasonIndex, x.Day });
        builder.HasOne<PlayerSeason>().WithMany().HasForeignKey(x => new { x.PlayerId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.SeasonIndex, x.Day });
    }
}
internal sealed class FootballStatisticConfiguration : IEntityTypeConfiguration<FootballPlayerStatistic>
{
    public void Configure(EntityTypeBuilder<FootballPlayerStatistic> builder)
    {
        builder.ToTable("FootballPlayerStatistics");
        builder.HasKey(x => new { x.PlayerId, x.SeasonIndex });
        builder.HasOne<PlayerSeason>().WithMany().HasForeignKey(x => new { x.PlayerId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.Games, game =>
        {
            game.ToTable("FootballStatisticGames");
            game.WithOwner().HasForeignKey("PlayerId", "SeasonIndex");
            game.HasKey("PlayerId", "SeasonIndex", nameof(FootballStatisticGame.Day));
            game.Property(x => x.Day).ValueGeneratedNever();
        });
    }
}
internal sealed class FootballClubRankConfiguration : IEntityTypeConfiguration<FootballClubRank>
{
    public void Configure(EntityTypeBuilder<FootballClubRank> builder)
    {
        builder.ToTable("FootballClubRanks");
        builder.HasKey(x => new { x.SeasonIndex, x.ClubId });
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
    }
}
