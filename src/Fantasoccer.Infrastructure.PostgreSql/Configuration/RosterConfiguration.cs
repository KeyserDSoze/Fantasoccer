using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class RosterSnapshotConfiguration : IEntityTypeConfiguration<RosterSnapshot>
{
    private static readonly string[] Scope = ["GroupId", "BasketId", "SeasonIndex", "TeamId", "Kind", "Day", "CompetitionId"];
    public void Configure(EntityTypeBuilder<RosterSnapshot> builder)
    {
        builder.ToTable("RosterSnapshots", table =>
        {
            table.HasCheckConstraint("CK_RosterSnapshots_KindDay", "(\"Kind\" IN (0,2) AND \"Day\" = 0) OR (\"Kind\" IN (1,3) AND \"Day\" BETWEEN 1 AND 38)");
            table.HasCheckConstraint("CK_RosterSnapshots_Competition", "(\"Kind\" IN (0,1) AND \"LeagueId\" IS NULL AND \"CompetitionId\" = '00000000-0000-0000-0000-000000000000') OR (\"Kind\" IN (2,3) AND \"LeagueId\" IS NOT NULL AND \"LeagueId\" = \"CompetitionId\")");
            table.HasCheckConstraint("CK_RosterSnapshots_OriginYear", "(\"Kind\" IN (0,1) AND \"TeamOriginYear\" = \"SeasonIndex\") OR (\"Kind\" IN (2,3) AND \"TeamOriginYear\" = \"SeasonIndex\" - 1)");
        });
        builder.HasKey(Scope);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.Property(x => x.LegacyLastUpdateLocal).HasColumnType("timestamp without time zone");
        builder.HasOne<FantasyTeamSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.BasketId, x.TeamOriginYear, x.TeamId })
            .HasPrincipalKey(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.OwnerUserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupLeague>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<LeagueSeason>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.GroupId, x.BasketId, x.SeasonIndex, x.Kind, x.Day, x.CompetitionId, x.OwnerUserId }).IsUnique().HasFilter("\"Kind\" IN (1,3)");
        builder.OwnsMany(x => x.Players, row =>
        {
            OwnedScope(row, "RosterPlayers", "Ordinal");
            row.HasOne(x => x.Player).WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
            row.HasOne<PlayerSeason>().WithMany().HasForeignKey("PlayerId", "SeasonIndex").OnDelete(DeleteBehavior.Restrict);
            row.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
            row.HasIndex([.. Scope, "PlayerId"]).IsUnique().HasFilter("\"Status\" = 0");
        });
        builder.OwnsMany(x => x.AdditionalOwners, row =>
        {
            OwnedScope(row, "RosterOwners", "UserId");
            row.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            row.HasOne<GroupMembership>().WithMany().HasForeignKey("GroupId", "UserId").OnDelete(DeleteBehavior.Restrict);
        });
        builder.OwnsMany(x => x.FormationHistory, row =>
        {
            OwnedScope(row, "RosterFormationChanges", "Ordinal");
            row.Property(x => x.LegacyDateLocal).HasColumnType("timestamp without time zone");
            row.OwnsMany(x => x.Players, player =>
            {
                player.ToTable("RosterFormationPlayerChanges");
                foreach (var field in Scope) player.Property(field == "SeasonIndex" || field == "Day" ? typeof(int) : field == "Kind" ? typeof(RosterKind) : typeof(Guid), field);
                player.Property<int>("ChangeOrdinal");
                player.WithOwner().HasForeignKey([.. Scope, "ChangeOrdinal"]);
                player.HasKey([.. Scope, "ChangeOrdinal", "Ordinal"]);
                player.Property(x => x.Ordinal).ValueGeneratedNever();
            });
        });
    }
    private static void OwnedScope<T>(OwnedNavigationBuilder<RosterSnapshot, T> row, string table, string key) where T : class
    {
        row.ToTable(table);
        foreach (var field in Scope) row.Property(field == "SeasonIndex" || field == "Day" ? typeof(int) : field == "Kind" ? typeof(RosterKind) : typeof(Guid), field);
        row.WithOwner().HasForeignKey(Scope);
        row.HasKey([.. Scope, key]);
        row.Property(key == "UserId" ? typeof(Guid) : typeof(int), key).ValueGeneratedNever();
    }
}

internal sealed class FootballMatchdayConfiguration : IEntityTypeConfiguration<FootballMatchday>
{
    public void Configure(EntityTypeBuilder<FootballMatchday> builder)
    {
        builder.ToTable("FootballMatchdays", table => table.HasCheckConstraint("CK_FootballMatchdays_Day", "\"SerieADay\" BETWEEN 1 AND 38"));
        builder.HasKey(x => new { x.SeasonIndex, x.SerieADay });
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Matches).WithOne().HasForeignKey(x => new { x.SeasonIndex, x.SerieADay }).OnDelete(DeleteBehavior.Restrict);
    }
}
internal sealed class FootballMatchConfiguration : IEntityTypeConfiguration<FootballMatch>
{
    public void Configure(EntityTypeBuilder<FootballMatch> builder)
    {
        builder.ToTable("FootballMatches");
        builder.HasKey(x => new { x.SeasonIndex, x.SerieADay, x.Ordinal });
        builder.Property(x => x.LegacyKickoffLocal).HasColumnType("timestamp without time zone");
        builder.HasOne(x => x.HomeClub).WithMany().HasForeignKey(x => x.HomeClubId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AwayClub).WithMany().HasForeignKey(x => x.AwayClubId).OnDelete(DeleteBehavior.Restrict);
    }
}
