using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class PushDayCheckpointConfiguration : IEntityTypeConfiguration<PushDayCheckpoint>
{
    public void Configure(EntityTypeBuilder<PushDayCheckpoint> builder)
    {
        builder.ToTable("PushDayCheckpoints", table => table.HasCheckConstraint("CK_PushDayCheckpoints_Day", "\"Day\" BETWEEN 0 AND 38"));
        builder.HasKey(x => new { x.SeasonIndex, x.Day });
        builder.Property(x => x.Day).ValueGeneratedNever();
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.Votes, vote =>
        {
            vote.ToTable("PushVoteBaselines"); vote.WithOwner().HasForeignKey("SeasonIndex", "Day");
            vote.HasKey("SeasonIndex", "Day", "Ordinal"); vote.Property(x => x.Ordinal).ValueGeneratedNever();
            vote.HasOne<PlayerSeason>().WithMany().HasForeignKey("PlayerId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            vote.HasOne<FootballClub>().WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.OwnsMany(x => x.FormationReceipts, receipt =>
        {
            receipt.ToTable("LegacyFormationPushReceipts"); receipt.WithOwner().HasForeignKey("SeasonIndex", "Day");
            receipt.HasKey("SeasonIndex", "Day", "Ordinal"); receipt.Property(x => x.Ordinal).ValueGeneratedNever();
        });
    }
}
internal sealed class LegacyPushActiveDayConfiguration : IEntityTypeConfiguration<LegacyPushActiveDay>
{
    public void Configure(EntityTypeBuilder<LegacyPushActiveDay> builder)
    {
        builder.ToTable("LegacyPushActiveDays"); builder.HasKey(x => x.Ordinal); builder.Property(x => x.Ordinal).ValueGeneratedNever();
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class PushDeliveryConfiguration : IEntityTypeConfiguration<PushDelivery>
{
    public void Configure(EntityTypeBuilder<PushDelivery> builder)
    {
        builder.ToTable("PushDeliveries", table =>
        {
            table.HasCheckConstraint("CK_PushDeliveries_Scope", "(\"GroupId\" IS NULL) = (\"LeagueId\" IS NULL) AND (\"TeamId\" IS NULL) = (\"BasketId\" IS NULL) AND (\"TeamId\" IS NULL OR \"GroupId\" IS NOT NULL)");
            table.HasCheckConstraint("CK_PushDeliveries_Day", "\"Day\" BETWEEN 0 AND 38");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.DeduplicationKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.LeaseUntilUtc });
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeagueSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.Receipts, receipt =>
        {
            receipt.ToTable("PushEndpointReceipts"); receipt.WithOwner().HasForeignKey("DeliveryId");
            receipt.HasKey("DeliveryId", "Ordinal"); receipt.Property(x => x.Ordinal).ValueGeneratedNever();
        });
    }
}
internal sealed class PushScopeConfirmationConfiguration : IEntityTypeConfiguration<PushDeliveryScopeConfirmation>
{
    public void Configure(EntityTypeBuilder<PushDeliveryScopeConfirmation> builder)
    {
        builder.ToTable("PushDeliveryScopeConfirmations"); builder.HasKey(x => x.Id);
        builder.HasOne<PushDelivery>().WithMany().HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeagueSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class PushFormationChangeReceiptConfiguration : IEntityTypeConfiguration<PushFormationChangeReceipt>
{
    public void Configure(EntityTypeBuilder<PushFormationChangeReceipt> builder)
    {
        builder.ToTable("PushFormationChangeReceipts");
        builder.HasKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId, x.Day, x.ChangeOrdinal });
        builder.Property(x => x.ChangeOrdinal).ValueGeneratedNever();
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
