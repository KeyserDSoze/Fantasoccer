using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class BasketDerivationConfiguration : IEntityTypeConfiguration<BasketDerivation>
{
    public void Configure(EntityTypeBuilder<BasketDerivation> builder)
    {
        builder.ToTable("BasketDerivations", table => table.HasCheckConstraint("CK_BasketDerivations_Kinds",
            "\"DerivedKind\" = 1 AND \"OriginKind\" = 0 AND \"BasketId\" <> \"OriginBasketId\""));
        builder.HasKey(x => new { x.GroupId, x.BasketId });
        builder.HasAlternateKey(x => new { x.GroupId, x.BasketId, x.OriginBasketId });
        builder.Property(x => x.DerivedKind).HasDefaultValue(BasketKind.Derived).ValueGeneratedNever();
        builder.Property(x => x.OriginKind).HasDefaultValue(BasketKind.Autonomous).ValueGeneratedNever();
        // Kinds participate in the keys: the derived basket must be Derived and the
        // origin Autonomous, and neither kind can change while the link exists.
        builder.HasOne<GroupBasket>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.DerivedKind })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.Kind }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupBasket>().WithMany().HasForeignKey(x => new { x.GroupId, x.OriginBasketId, x.OriginKind })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.Kind }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GroupId, x.OriginBasketId });
    }
}

internal sealed class TeamDerivationConfiguration : IEntityTypeConfiguration<TeamDerivation>
{
    public void Configure(EntityTypeBuilder<TeamDerivation> builder)
    {
        builder.ToTable("TeamDerivations", table => table.HasCheckConstraint("CK_TeamDerivations_Distinct", "\"TeamId\" <> \"OriginTeamId\""));
        builder.HasKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId });
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        // Same group and season as the derived team, inside the origin basket that
        // the derived basket declares.
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.OriginBasketId, x.SeasonIndex, x.OriginTeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BasketDerivation>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.OriginBasketId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.OriginBasketId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.OriginTeamId }).IsUnique();
        builder.HasIndex(x => new { x.GroupId, x.TeamId }).IsUnique();
        builder.HasIndex(x => new { x.GroupId, x.OriginTeamId });
    }
}

internal sealed class LeagueDerivationConfiguration : IEntityTypeConfiguration<LeagueDerivation>
{
    public void Configure(EntityTypeBuilder<LeagueDerivation> builder)
    {
        builder.ToTable("LeagueDerivations", table =>
        {
            table.HasCheckConstraint("CK_LeagueDerivations_Distinct", "\"LeagueId\" <> \"SourceLeagueId\"");
            table.HasCheckConstraint("CK_LeagueDerivations_Qualified", "\"QualifiedTeams\" BETWEEN 1 AND 64");
        });
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SourceLeagueId });
        builder.Property(x => x.Stage).HasMaxLength(200);
        builder.HasOne<LeagueSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        // Composite FK with the same GroupId/SeasonIndex: cross-group and
        // cross-season sources are impossible, even in raw SQL.
        builder.HasOne<LeagueSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.SourceLeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GroupId, x.SourceLeagueId, x.SeasonIndex });
    }
}

internal sealed class LeagueParticipationConfiguration : IEntityTypeConfiguration<LeagueParticipation>
{
    public void Configure(EntityTypeBuilder<LeagueParticipation> builder)
    {
        builder.ToTable("LeagueParticipations", table =>
        {
            table.HasCheckConstraint("CK_LeagueParticipations_Source", "(\"Source\" = 2) = (\"SourceLeagueId\" IS NOT NULL) AND (\"QualificationRank\" IS NULL OR \"Source\" = 2)");
            table.HasCheckConstraint("CK_LeagueParticipations_Rank", "\"QualificationRank\" IS NULL OR \"QualificationRank\" BETWEEN 1 AND 64");
        });
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId });
        // Only teams of a basket linked to this league season can be admitted.
        builder.HasOne<LeagueBasketSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.BasketId, x.SeasonIndex }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.TeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeagueDerivation>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.SourceLeagueId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.AdmittedByUserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GroupId, x.TeamId });
    }
}

internal sealed class LeagueParticipantOwnerConfiguration : IEntityTypeConfiguration<LeagueParticipantOwner>
{
    public void Configure(EntityTypeBuilder<LeagueParticipantOwner> builder)
    {
        builder.ToTable("LeagueParticipantOwners");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.UserId });
        builder.HasOne<LeagueParticipation>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId });
    }
}

internal sealed class LeagueLineupAutoCopyConfiguration : IEntityTypeConfiguration<LeagueLineupAutoCopy>
{
    public void Configure(EntityTypeBuilder<LeagueLineupAutoCopy> builder)
    {
        builder.ToTable("LeagueLineupAutoCopies");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId });
        builder.HasOne<LeagueParticipation>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.EnabledByUserId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LeagueLineupConfiguration : IEntityTypeConfiguration<LeagueLineup>
{
    private static readonly string[] Scope = ["GroupId", "LeagueId", "SeasonIndex", "TeamId", "Day"];
    public void Configure(EntityTypeBuilder<LeagueLineup> builder)
    {
        builder.ToTable("LeagueLineups", table => table.HasCheckConstraint("CK_LeagueLineups_Day", "\"Day\" BETWEEN 1 AND 38"));
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId, x.Day });
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<LeagueParticipation>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex, x.TeamId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.Players, row =>
        {
            row.ToTable("LeagueLineupPlayers", table => table.HasCheckConstraint("CK_LeagueLineupPlayers_Position", "\"Position\" BETWEEN 0 AND 11"));
            ScopeColumns(row);
            row.WithOwner().HasForeignKey(Scope);
            row.HasKey([.. Scope, "PlayerId"]);
            row.HasOne<FootballPlayer>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.OwnsMany(x => x.History, row =>
        {
            row.ToTable("LeagueLineupChanges");
            ScopeColumns(row);
            row.WithOwner().HasForeignKey(Scope);
            row.HasKey([.. Scope, "Ordinal"]);
            row.Property(x => x.Ordinal).ValueGeneratedNever();
            row.Property(x => x.Author).HasMaxLength(200);
            row.OwnsMany(x => x.Players, player =>
            {
                player.ToTable("LeagueLineupChangePlayers");
                foreach (var field in Scope) player.Property(Type(field), field);
                player.Property<int>("ChangeOrdinal");
                player.WithOwner().HasForeignKey([.. Scope, "ChangeOrdinal"]);
                player.HasKey([.. Scope, "ChangeOrdinal", "Ordinal"]);
                player.Property(x => x.Ordinal).ValueGeneratedNever();
                player.Property(x => x.PlayerName).HasMaxLength(200);
            });
        });
    }
    private static void ScopeColumns<T>(OwnedNavigationBuilder<LeagueLineup, T> row) where T : class
    {
        foreach (var field in Scope) row.Property(Type(field), field);
    }
    private static Type Type(string field) => field is "SeasonIndex" or "Day" ? typeof(int) : typeof(Guid);
}
