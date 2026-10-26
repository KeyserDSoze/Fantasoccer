using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class CardLibraryConfiguration : IEntityTypeConfiguration<CardLibrary>
{
    public void Configure(EntityTypeBuilder<CardLibrary> builder) { builder.ToTable("CardLibraries"); builder.HasKey(x => x.Name); }
}
internal sealed class RuleCardConfiguration : IEntityTypeConfiguration<RuleCard>
{
    public void Configure(EntityTypeBuilder<RuleCard> builder)
    {
        builder.ToTable("RuleCards");
        builder.HasKey(x => new { x.Library, x.Id });
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<CardLibrary>().WithMany().HasForeignKey(x => x.Library).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsOne(x => x.MyCondition, condition => Condition(condition, "My"));
        builder.OwnsOne(x => x.EnemyCondition, condition => Condition(condition, "Enemy"));
    }
    private static void Condition(OwnedNavigationBuilder<RuleCard, RuleCardCondition> condition, string side)
    {
        condition.OwnsOne(x => x.MinimumPlayer); condition.OwnsOne(x => x.MaximumPlayer);
        condition.OwnsOne(x => x.MinimumTeam); condition.OwnsOne(x => x.MaximumTeam);
        condition.OwnsMany(x => x.Roles, role =>
        {
            role.ToTable("RuleCard" + side + "Roles");
            role.WithOwner().HasForeignKey("Library", "CardId");
            role.HasKey("Library", "CardId", "Ordinal"); role.Property(x => x.Ordinal).ValueGeneratedNever();
        });
    }
}
