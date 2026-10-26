using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class ModelTests
{
    [Fact]
    public void Snapshot_matches_the_current_model()
    {
        using var database = new FantasoccerDbContext(new DbContextOptionsBuilder<FantasoccerDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=unused").Options);

        Assert.False(database.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Membership_and_seasonal_data_have_composite_keys()
    {
        using var database = new FantasoccerDbContext(new DbContextOptionsBuilder<FantasoccerDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only;Username=unused").Options);

        Assert.Equal(["GroupId", "UserId"], Key<GroupMembership>());
        Assert.Equal(["PlayerId", "SeasonIndex"], Key<PlayerSeason>());
        Assert.Equal(["GroupId", "BasketId", "SeasonIndex"], Key<BasketSeason>());
        Assert.Equal(["GroupId", "LeagueId", "SeasonIndex"], Key<LeagueSeason>());
        Assert.Equal(["GroupId", "LeagueId", "BasketId", "SeasonIndex"], Key<LeagueBasketSeason>());

        string[] Key<TEntity>() => database.Model.FindEntityType(typeof(TEntity))!.FindPrimaryKey()!
            .Properties.Select(property => property.Name).ToArray();
    }
}
