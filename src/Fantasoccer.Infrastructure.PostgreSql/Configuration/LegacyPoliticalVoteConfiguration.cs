using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class LegacyPoliticalVoteConfiguration : IEntityTypeConfiguration<LegacyPoliticalVoteEntry>
{
    public void Configure(EntityTypeBuilder<LegacyPoliticalVoteEntry> builder)
    {
        builder.ToTable("LegacyPoliticalVoteEntries", table => table.HasCheckConstraint("CK_LegacyPoliticalVotes_Day", "\"Day\" BETWEEN 1 AND 38"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SourceId).HasMaxLength(256);
        builder.Property(x => x.DocumentSha256).HasMaxLength(64);
        builder.HasIndex(x => new { x.SourceId, x.DocumentSha256, x.SourceOrdinal }).IsUnique();
        builder.HasIndex(x => new { x.SeasonIndex, x.Day });
        builder.HasOne<PlayerSeason>().WithMany().HasForeignKey(x => new { x.PlayerId, x.SeasonIndex }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.PlayerId).HasColumnName("PlayerKey").HasMaxLength(200);
        builder.Property(x => x.ClubId).HasColumnName("ClubCode").HasMaxLength(3);
    }
}
