using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.ToTable("Seasons", table =>
        {
            table.HasCheckConstraint("CK_Seasons_PositiveIndex", "\"SeasonIndex\" > 0");
            table.HasCheckConstraint("CK_Seasons_StartYear", "\"StartYear\" = \"SeasonIndex\" + 2011");
        });
        builder.HasKey(x => x.SeasonIndex);
        builder.Property(x => x.SeasonIndex).ValueGeneratedNever();
        builder.HasIndex(x => x.StartYear).IsUnique();
    }
}

internal sealed class FootballPlayerConfiguration : IEntityTypeConfiguration<FootballPlayer>
{
    public void Configure(EntityTypeBuilder<FootballPlayer> builder)
    {
        builder.ToTable("Players");
        builder.HasKey(x => x.Id);
        // Uniqueness is seasonal, enforced by PlayerSeasons.PlayerKey.
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}

internal sealed class FootballClubConfiguration : IEntityTypeConfiguration<FootballClub>
{
    public void Configure(EntityTypeBuilder<FootballClub> builder)
    {
        builder.ToTable("Clubs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
    }
}

internal sealed class PlayerSeasonConfiguration : IEntityTypeConfiguration<PlayerSeason>
{
    public void Configure(EntityTypeBuilder<PlayerSeason> builder)
    {
        builder.ToTable("PlayerSeasons", table =>
        {
            table.HasCheckConstraint("CK_PlayerSeasons_Position", "\"Position\" >= -1 AND \"Position\" <= 3");
            table.HasCheckConstraint("CK_PlayerSeasons_NormalizedName", "\"NormalizedName\" ~ '^[a-z]+$'");
        });
        builder.HasKey(x => new { x.PlayerId, x.SeasonIndex });
        builder.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PlayerKey).HasMaxLength(220)
            .HasComputedColumnSql("\"SeasonIndex\"::text || ':' || \"NormalizedName\"", stored: true);
        builder.HasIndex(x => x.PlayerKey).IsUnique();
        builder.HasOne(x => x.Player).WithMany(x => x.Seasons).HasForeignKey(x => x.PlayerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Season).WithMany().HasForeignKey(x => x.SeasonIndex)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
