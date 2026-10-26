using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class AuctionSessionConfiguration : IEntityTypeConfiguration<AuctionSession>
{
    private static readonly string[] Scope = ["GroupId", "LeagueId", "SeasonIndex", "AuctionId"];
    public void Configure(EntityTypeBuilder<AuctionSession> builder)
    {
        builder.ToTable("AuctionSessions");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.Id });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasIndex(x => x.Id).IsUnique();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.LegacyCreatedUtc).HasColumnType("timestamp without time zone");
        builder.HasOne<LeagueSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.CreatorUserId }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.PlayerOrder, row =>
        {
            row.ToTable("AuctionPlayerEntries");
            row.WithOwner().HasForeignKey(Scope);
            row.HasKey([.. Scope, "Role", "Ordinal"]);
            row.Property(x => x.Role).ValueGeneratedNever(); row.Property(x => x.Ordinal).ValueGeneratedNever();
            row.HasOne<PlayerSeason>().WithMany().HasForeignKey("PlayerId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
        });
        builder.OwnsMany(x => x.LastShown, row =>
        {
            row.ToTable("AuctionLastShown"); row.WithOwner().HasForeignKey(Scope); row.HasKey([.. Scope, "Role"]);
            row.Property(x => x.Role).ValueGeneratedNever();
            row.HasOne<PlayerSeason>().WithMany().HasForeignKey("PlayerId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
        });
        builder.OwnsMany(x => x.Winnings, row =>
        {
            row.ToTable("AuctionWinnings", table => table.HasCheckConstraint("CK_AuctionWinnings_Team", "(\"TeamId\" IS NULL) = (\"BasketId\" IS NULL)"));
            row.WithOwner().HasForeignKey(Scope); row.HasKey([.. Scope, "Ordinal"]); row.Property(x => x.Ordinal).ValueGeneratedNever();
            row.HasOne<PlayerSeason>().WithMany().HasForeignKey("PlayerId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            row.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey("GroupId", "BasketId", "SeasonIndex", "TeamId")
                .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
            row.HasOne<LeagueBasketSeason>().WithMany().HasForeignKey("GroupId", "LeagueId", "BasketId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
        });
        builder.OwnsMany(x => x.LegacyParticipants, row =>
        {
            row.ToTable("AuctionLegacyParticipants"); row.WithOwner().HasForeignKey(Scope); row.HasKey([.. Scope, "Ordinal"]);
            row.Property(x => x.Ordinal).ValueGeneratedNever();
            row.OwnsMany(x => x.CoOwners, owner =>
            {
                owner.ToTable("AuctionLegacyParticipantCoOwners"); owner.WithOwner().HasForeignKey([.. Scope, "ParticipantOrdinal"]);
                owner.HasKey([.. Scope, "ParticipantOrdinal", "Ordinal"]); owner.Property(x => x.Ordinal).ValueGeneratedNever();
            });
        });
        builder.OwnsOne(x => x.Actual, lot =>
        {
            lot.ToTable("AuctionCurrentLots", table => table.HasCheckConstraint("CK_AuctionCurrentLots_Team", "(\"WinningTeamId\" IS NULL) = (\"WinningBasketId\" IS NULL)"));
            lot.WithOwner().HasForeignKey(Scope);
            lot.Property(x => x.LegacyStartedUtc).HasColumnType("timestamp without time zone");
            lot.Property(x => x.LegacyTimerStartedUtc).HasColumnType("timestamp without time zone");
            lot.HasOne<PlayerSeason>().WithMany().HasForeignKey("PlayerId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            lot.HasOne<PlayerSeason>().WithMany().HasForeignKey("SubstitutedPlayerId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            lot.HasOne<FootballClub>().WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
            lot.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey("GroupId", "WinningBasketId", "SeasonIndex", "WinningTeamId")
                .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
            lot.HasOne<LeagueBasketSeason>().WithMany().HasForeignKey("GroupId", "LeagueId", "WinningBasketId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            lot.OwnsOne(x => x.Statistics, stats =>
            {
                stats.OwnsMany(x => x.Games, game =>
                {
                    game.ToTable("AuctionCurrentStatisticGames"); game.WithOwner().HasForeignKey(Scope); game.HasKey([.. Scope, "Ordinal"]);
                    game.Property(x => x.Ordinal).ValueGeneratedNever();
                });
            });
        });
    }
}
internal sealed class AuctionPresenceConfiguration : IEntityTypeConfiguration<AuctionPresence>
{
    public void Configure(EntityTypeBuilder<AuctionPresence> builder)
    {
        builder.ToTable("AuctionPresence");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId, x.ConnectionId });
        builder.HasOne<AuctionSession>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.ConnectionId);
    }
}

internal sealed class AuctionRosterChangeConfiguration : IEntityTypeConfiguration<AuctionRosterChange>
{
    public void Configure(EntityTypeBuilder<AuctionRosterChange> builder)
    {
        builder.ToTable("AuctionRosterChanges");
        builder.HasKey(x => x.Id);
        builder.HasOne<AuctionSession>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.AuctionId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PlayerSeason>().WithMany().HasForeignKey(x => new { x.PlayerId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.ActorUserId }).OnDelete(DeleteBehavior.Restrict);
    }
}
