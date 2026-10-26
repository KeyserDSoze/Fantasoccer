using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class AccountPreferencesConfiguration : IEntityTypeConfiguration<AccountPreferences>
{
    public void Configure(EntityTypeBuilder<AccountPreferences> builder)
    {
        builder.ToTable("AccountPreferences");
        builder.HasKey(x => x.UserId);
        builder.HasOne<UserAccount>().WithOne().HasForeignKey<AccountPreferences>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class AccountSelectionConfiguration : IEntityTypeConfiguration<AccountSelection>
{
    public void Configure(EntityTypeBuilder<AccountSelection> builder)
    {
        builder.ToTable("AccountSelections", table =>
        {
            table.HasCheckConstraint("CK_AccountSelections_LeagueGroup", "\"LeagueId\" IS NULL OR \"GroupId\" IS NOT NULL");
            table.HasCheckConstraint("CK_AccountSelections_WorkingGroup", "\"WorkingUserId\" IS NULL OR \"GroupId\" IS NOT NULL");
        });
        builder.HasKey(x => x.UserId);
        builder.HasOne<UserAccount>().WithOne().HasForeignKey<AccountSelection>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.UserId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupLeague>().WithMany().HasForeignKey(x => new { x.GroupId, x.LeagueId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.Year).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.WorkingUser).WithMany().HasForeignKey(x => x.WorkingUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<GroupMembership>().WithMany().HasForeignKey(x => new { x.GroupId, x.WorkingUserId }).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UnresolvedAccountSelectionConfiguration : IEntityTypeConfiguration<UnresolvedAccountSelection>
{
    public void Configure(EntityTypeBuilder<UnresolvedAccountSelection> builder)
    {
        builder.ToTable("UnresolvedAccountSelections");
        builder.HasKey(x => x.UserId);
        builder.HasOne<UserAccount>().WithOne().HasForeignKey<UnresolvedAccountSelection>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LeagueRulesConfiguration : IEntityTypeConfiguration<LeagueRules>
{
    public void Configure(EntityTypeBuilder<LeagueRules> builder)
    {
        builder.ToTable("LeagueRules");
        builder.HasKey(x => new { x.GroupId, x.LeagueId, x.SeasonIndex });
        builder.HasOne<LeagueSeason>().WithOne(x => x.Rules)
            .HasForeignKey<LeagueRules>(x => new { x.GroupId, x.LeagueId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.Votes, row =>
        {
            Scope(row, "LeagueVoteWeights", "PlayerRole");
        });
        builder.OwnsMany(x => x.Rounds, row => Scope(row, "LeagueStageRules", "Ordinal"));
        builder.OwnsMany(x => x.CardLimits, row => Scope(row, "LeagueCardLimits", "CardType"));
        builder.OwnsMany(x => x.OpeningPrizes, row => Scope(row, "LeagueOpeningPrizes", "Position"));
        builder.OwnsMany(x => x.PreviousLeagues, row =>
        {
            Scope(row, "PreviousLeagueQualifications", "SourceLeagueId");
            row.HasOne<GroupLeague>().WithMany().HasForeignKey("GroupId", "SourceLeagueId").OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void Scope<T>(OwnedNavigationBuilder<LeagueRules, T> row, string table, string component) where T : class
    {
        row.ToTable(table);
        row.Property<Guid>("GroupId");
        row.Property<Guid>("LeagueId");
        row.Property<int>("SeasonIndex");
        row.WithOwner().HasForeignKey("GroupId", "LeagueId", "SeasonIndex");
        row.HasKey("GroupId", "LeagueId", "SeasonIndex", component);
        row.Property(component == "SourceLeagueId" ? typeof(Guid) : typeof(int), component).ValueGeneratedNever();
    }
}
