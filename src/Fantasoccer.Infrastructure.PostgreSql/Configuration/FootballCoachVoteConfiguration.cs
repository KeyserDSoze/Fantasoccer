using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Fantasoccer.Infrastructure.PostgreSql.Configuration;

internal sealed class FootballCoachVoteConfiguration : IEntityTypeConfiguration<FootballCoachVote>
{
    public void Configure(EntityTypeBuilder<FootballCoachVote> builder)
    {
        builder.ToTable("FootballCoachVotes", table =>
        {
            table.HasCheckConstraint("CK_FootballCoachVotes_Day", "\"Day\" BETWEEN 1 AND 38");
            table.HasCheckConstraint("CK_FootballCoachVotes_Role", "\"Role\" = 4");
        });
        builder.HasKey(x => new { x.SeasonIndex, x.Day, x.ClubId, x.CoachKey });
        builder.Property(x => x.CoachKey).HasMaxLength(200);
        builder.Property(x => x.Name).HasMaxLength(300);
        builder.Property(x => x.ProviderVoteJson).HasColumnType("jsonb");
        builder.HasOne<Season>().WithMany().HasForeignKey(x => x.SeasonIndex).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Club).WithMany().HasForeignKey(x => x.ClubId).OnDelete(DeleteBehavior.Restrict);
    }
}
