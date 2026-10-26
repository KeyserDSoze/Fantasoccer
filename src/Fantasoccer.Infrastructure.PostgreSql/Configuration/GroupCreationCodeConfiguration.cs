using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class GroupCreationCodeConfiguration : IEntityTypeConfiguration<GroupCreationCode>
{
    public void Configure(EntityTypeBuilder<GroupCreationCode> builder)
    {
        builder.ToTable("GroupCreationCodes", table =>
        {
            table.HasCheckConstraint("CK_GroupCreationCodes_Redemption", """
                ("RedeemedAtUtc" IS NULL AND "RedeemedByUserId" IS NULL AND "RedeemedGroupId" IS NULL) OR
                ("RedeemedAtUtc" IS NOT NULL AND "RedeemedByUserId" IS NOT NULL AND "RedeemedGroupId" IS NOT NULL AND "RevokedAtUtc" IS NULL)
                """);
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodeHash).HasMaxLength(64);
        builder.Property(x => x.Label).HasMaxLength(200);
        builder.HasIndex(x => x.CodeHash).IsUnique();
        builder.HasIndex(x => x.CreatedAtUtc);
        builder.HasIndex(x => x.RedeemedGroupId).IsUnique();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.RedeemedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<FantasyGroup>().WithMany().HasForeignKey(x => x.RedeemedGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
