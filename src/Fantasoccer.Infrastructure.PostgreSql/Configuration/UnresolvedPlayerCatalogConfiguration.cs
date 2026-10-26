using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class UnresolvedPlayerCatalogConfiguration : IEntityTypeConfiguration<UnresolvedPlayerCatalogEntry>
{
    public void Configure(EntityTypeBuilder<UnresolvedPlayerCatalogEntry> builder)
    {
        builder.ToTable("UnresolvedPlayerCatalogEntries", table =>
            table.HasCheckConstraint("CK_UnresolvedPlayerCatalogEntries_Ordinal", "\"SourceOrdinal\" >= 0"));
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.SourceId, x.DocumentSha256, x.SourceOrdinal }).IsUnique();
        builder.Property(x => x.DocumentSha256).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ArchiveReference).HasMaxLength(80).IsRequired();
        builder.Property(x => x.JsonPath).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(64).IsRequired();
        // Text, rather than JSONB, retains the exact source fragment, including
        // unknown fields, duplicate properties, escapes and numeric spelling.
        builder.Property(x => x.OriginalPayloadJson).HasColumnType("text").IsRequired();
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Restrict);
    }
}
