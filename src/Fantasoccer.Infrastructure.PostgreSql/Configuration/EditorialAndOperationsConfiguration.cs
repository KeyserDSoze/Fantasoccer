using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class NewsEditionConfiguration : IEntityTypeConfiguration<NewsEdition>
{
    public void Configure(EntityTypeBuilder<NewsEdition> builder)
    {
        builder.ToTable("NewsEditions", table => table.HasCheckConstraint("CK_NewsEditions_Day", "\"Day\" BETWEEN 1 AND 38"));
        builder.HasKey(x => new { x.GroupId, x.SeasonIndex, x.Day }); builder.Property(x => x.Day).ValueGeneratedNever();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<FantasyGroup>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.Articles, article =>
        {
            article.ToTable("NewsArticles"); article.WithOwner().HasForeignKey("GroupId", "SeasonIndex", "Day");
            article.HasKey("GroupId", "SeasonIndex", "Day", "Ordinal"); article.Property(x => x.Ordinal).ValueGeneratedNever();
            article.Property(x => x.LegacyDate).HasColumnType("timestamp without time zone");
            article.HasOne<LeagueSeason>().WithMany().HasForeignKey("GroupId", "LeagueId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            article.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
internal sealed class TeamAdviceConfiguration : IEntityTypeConfiguration<TeamAdvice>
{
    public void Configure(EntityTypeBuilder<TeamAdvice> builder)
    {
        builder.ToTable("TeamAdvice", table => table.HasCheckConstraint("CK_TeamAdvice_Day", "\"Day\" BETWEEN 1 AND 38"));
        builder.HasKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Day, x.OwnerUserId }); builder.Property(x => x.Day).ValueGeneratedNever();
        builder.Property(x => x.OwnerUserId).ValueGeneratedNever();
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.OwnerUserId }).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.GeneratedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class ArchivedLogConfiguration : IEntityTypeConfiguration<ArchivedOperationalLog>
{
    public void Configure(EntityTypeBuilder<ArchivedOperationalLog> builder)
    {
        builder.ToTable("ArchivedOperationalLogs"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => new { x.PartitionKey, x.RowKey }).IsUnique(); builder.HasIndex(x => x.TimestampUtc);
        builder.OwnsMany(x => x.Properties, property =>
        { property.ToTable("ArchivedOperationalLogProperties"); property.WithOwner().HasForeignKey("LogId"); property.HasKey("LogId", "Name"); });
    }
}
internal sealed class SqlJobStateConfiguration : IEntityTypeConfiguration<SqlJobState>
{
    public void Configure(EntityTypeBuilder<SqlJobState> builder)
    {
        builder.ToTable("SqlJobStates"); builder.HasKey(x => new { x.Name, x.SeasonIndex });
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class SqlJobRunConfiguration : IEntityTypeConfiguration<SqlJobRun>
{
    public void Configure(EntityTypeBuilder<SqlJobRun> builder)
    {
        builder.ToTable("SqlJobRuns"); builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasOne<SqlJobState>().WithMany().HasForeignKey(x => new { x.Name, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class HallRankContributionConfiguration : IEntityTypeConfiguration<HallRankContribution>
{
    public void Configure(EntityTypeBuilder<HallRankContribution> builder)
    {
        builder.ToTable("HallRankContributions"); builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.OwnerUserId });
        builder.HasOne<HallArchive>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<LeagueSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class HallBaselineConfirmationConfiguration : IEntityTypeConfiguration<HallBaselineConfirmation>
{
    public void Configure(EntityTypeBuilder<HallBaselineConfirmation> builder)
    {
        builder.ToTable("HallBaselineConfirmations"); builder.HasKey(x => x.Id);
        builder.HasOne<HallArchive>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.ActorUserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
