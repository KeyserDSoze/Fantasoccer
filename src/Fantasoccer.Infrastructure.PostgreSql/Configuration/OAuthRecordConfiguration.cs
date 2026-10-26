using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class OAuthRecordConfiguration : IEntityTypeConfiguration<OAuthRecord>
{
    public void Configure(EntityTypeBuilder<OAuthRecord> builder)
    {
        builder.ToTable("OAuthRecords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Payload).HasColumnType("jsonb");
        builder.HasIndex(x => x.Kind);
        builder.HasIndex(x => x.DeleteAfterUtc);
    }
}
