using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class FantasyTeamSeasonConfiguration : IEntityTypeConfiguration<FantasyTeamSeason>
{
    public void Configure(EntityTypeBuilder<FantasyTeamSeason> builder)
    {
        builder.ToTable("FantasyTeamSeasons");
        builder.HasKey(x => x.Id);
        builder.HasAlternateKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id });
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.NormalizedName }).IsUnique().HasFilter("NOT \"IsArchived\"");
        builder.HasOne(x => x.BasketSeason).WithMany()
            .HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class TeamOwnershipConfiguration : IEntityTypeConfiguration<TeamOwnership>
{
    public void Configure(EntityTypeBuilder<TeamOwnership> builder)
    {
        builder.ToTable("TeamOwnerships");
        builder.HasKey(x => new { x.GroupId, x.TeamId, x.UserId });
        builder.HasOne(x => x.Team).WithMany(x => x.Owners)
            .HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Membership).WithMany()
            .HasForeignKey(x => new { x.GroupId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GroupId, x.TeamId }).IsUnique().HasFilter("\"IsPrimary\"");
        builder.HasIndex(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.UserId }).IsUnique().HasFilter("\"IsPrimary\"");
    }
}

internal sealed class CoreMigrationStateConfiguration : IEntityTypeConfiguration<CoreMigrationState>
{
    public void Configure(EntityTypeBuilder<CoreMigrationState> builder)
    {
        builder.ToTable("CoreMigrationState", table => table.HasCheckConstraint("CK_CoreMigrationState_Singleton", "\"Id\" = 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
    }
}

internal sealed class TeamOwnershipChangeConfiguration : IEntityTypeConfiguration<TeamOwnershipChange>
{
    public void Configure(EntityTypeBuilder<TeamOwnershipChange> builder)
    {
        builder.ToTable("TeamOwnershipChanges");
        builder.HasKey(x => x.Id);
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GroupId, x.TeamId, x.ChangedAtUtc });
    }
}

internal sealed class RankCreditConfirmationConfiguration : IEntityTypeConfiguration<RankCreditConfirmation>
{
    public void Configure(EntityTypeBuilder<RankCreditConfirmation> builder)
    {
        builder.ToTable("RankCreditConfirmations");
        builder.HasKey(x => x.Id);
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.ActorUserId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LegacyIdentityMapConfiguration : IEntityTypeConfiguration<LegacyIdentityMap>
{
    public void Configure(EntityTypeBuilder<LegacyIdentityMap> builder)
    {
        builder.ToTable("LegacyIdentityMaps");
        builder.HasKey(x => new { x.SourceId, x.Kind, x.LegacyKey });
        builder.HasIndex(x => new { x.SourceId, x.Kind, x.TargetId });
    }
}

internal sealed class CoreImportCheckpointConfiguration : IEntityTypeConfiguration<CoreImportCheckpoint>
{
    public void Configure(EntityTypeBuilder<CoreImportCheckpoint> builder)
    {
        builder.ToTable("CoreImportCheckpoints");
        builder.HasKey(x => new { x.SourceId, x.Container, x.BlobName });
        builder.Property(x => x.Sha256).HasMaxLength(64).IsRequired();
    }
}
