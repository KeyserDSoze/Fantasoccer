using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
{
    public void Configure(EntityTypeBuilder<UserAccount> builder)
    {
        builder.ToTable("Users", table => table.HasCheckConstraint("CK_Users_EmailCanonical",
            "\"Email\" IS NULL OR (\"Email\" = lower(btrim(\"Email\")) AND length(\"Email\") > 0)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.IsEnabled).HasDefaultValue(true);
        builder.HasIndex(x => x.Email).IsUnique();
    }
}

internal sealed class FantasyGroupConfiguration : IEntityTypeConfiguration<FantasyGroup>
{
    public void Configure(EntityTypeBuilder<FantasyGroup> builder)
    {
        builder.ToTable("Groups");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Revision).HasDefaultValue(1).IsConcurrencyToken();
    }
}

internal sealed class GroupMembershipConfiguration : IEntityTypeConfiguration<GroupMembership>
{
    public void Configure(EntityTypeBuilder<GroupMembership> builder)
    {
        builder.ToTable("GroupMemberships", table => table.HasCheckConstraint("CK_GroupMemberships_Roles",
            "\"Roles\" >= 0 AND \"Roles\" <= 15"));
        builder.HasKey(x => new { x.GroupId, x.UserId });
        builder.HasOne(x => x.Group).WithMany(x => x.Memberships).HasForeignKey(x => x.GroupId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.User).WithMany(x => x.Memberships).HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
