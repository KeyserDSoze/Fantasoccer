using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class UserPushPreferencesConfiguration : IEntityTypeConfiguration<UserPushPreferences>
{
    public void Configure(EntityTypeBuilder<UserPushPreferences> builder)
    {
        builder.ToTable("UserPushPreferences"); builder.HasKey(x => x.UserId);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<UserAccount>().WithOne().HasForeignKey<UserPushPreferences>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.OwnsMany(x => x.LegacySubscriptions, row =>
        {
            row.ToTable("LegacyPushSubscriptions"); row.WithOwner().HasForeignKey("UserId");
            row.HasKey("UserId", "Ordinal"); row.Property(x => x.Ordinal).ValueGeneratedNever();
        });
    }
}
internal sealed class PushDeviceBindingConfiguration : IEntityTypeConfiguration<PushDeviceBinding>
{
    public void Configure(EntityTypeBuilder<PushDeviceBinding> builder)
    {
        builder.ToTable("PushDeviceBindings"); builder.HasKey(x => x.EndpointHash);
        builder.Property(x => x.Version).IsConcurrencyToken();
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.UserId);
    }
}
internal sealed class PushDeviceOwnershipEventConfiguration : IEntityTypeConfiguration<PushDeviceOwnershipEvent>
{
    public void Configure(EntityTypeBuilder<PushDeviceOwnershipEvent> builder)
    {
        builder.ToTable("PushDeviceOwnershipEvents"); builder.HasKey(x => x.Id);
        builder.HasOne<PushDeviceBinding>().WithMany().HasForeignKey(x => x.EndpointHash).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.PreviousUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserAccount>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
