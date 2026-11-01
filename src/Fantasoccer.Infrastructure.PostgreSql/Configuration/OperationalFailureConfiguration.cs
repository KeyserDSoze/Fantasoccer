using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class OperationalFailureConfiguration : IEntityTypeConfiguration<OperationalFailure>
{
    public void Configure(EntityTypeBuilder<OperationalFailure> builder)
    {
        builder.ToTable("OperationalFailures");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Source).HasMaxLength(100);
        builder.Property(x => x.Code).HasMaxLength(100);
        builder.HasIndex(x => x.OccurredAtUtc);
    }
}
