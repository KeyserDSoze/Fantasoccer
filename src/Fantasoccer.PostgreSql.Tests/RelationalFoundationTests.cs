using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed class RelationalFoundationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task Migration_is_repeatable_and_reversible()
    {
        await using var database = fixture.CreateContext();
        var migrations = (await database.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal(database.Database.GetMigrations().ToArray(), migrations);
        await database.Database.MigrateAsync();
        Assert.Equal(migrations, await database.Database.GetAppliedMigrationsAsync());

        await database.GetService<IMigrator>().MigrateAsync("0");
        Assert.Empty(await database.Database.GetAppliedMigrationsAsync());
        await database.Database.MigrateAsync();
        Assert.Equal(migrations, await database.Database.GetAppliedMigrationsAsync());
    }

    [PostgreSqlFact]
    public async Task One_user_can_have_different_roles_in_different_groups()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var first = new FantasyGroup { Name = "First group" };
        var second = new FantasyGroup { Name = "Second group" };
        var user = new UserAccount { Email = "friend@example.test", DisplayName = "Friend" };
        database.GroupMemberships.AddRange(
            new() { Group = first, User = user, Roles = GroupRole.Admin },
            new() { Group = second, User = user, Roles = GroupRole.Participant });
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var roles = await database.GroupMemberships.Where(x => x.UserId == user.Id)
            .ToDictionaryAsync(x => x.GroupId, x => x.Roles);
        Assert.Equal(GroupRole.Admin, roles[first.Id]);
        Assert.Equal(GroupRole.Participant, roles[second.Id]);
        Assert.Equal(1, await database.Users.CountAsync());
    }

    [PostgreSqlFact]
    public async Task Duplicate_membership_is_rejected_by_the_database()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var membership = new GroupMembership
        {
            Group = new() { Name = "Group" },
            User = new() { Email = "friend@example.test", DisplayName = "Friend" },
            Roles = GroupRole.Reader
        };
        database.Add(membership);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        database.Add(new GroupMembership { GroupId = membership.GroupId, UserId = membership.UserId, Roles = GroupRole.Admin });
        await AssertSqlStateAsync(database, PostgresErrorCodes.UniqueViolation);
    }

    [PostgreSqlFact]
    public async Task Same_local_identifiers_are_allowed_in_different_groups()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var leagueId = Guid.NewGuid();
        var basketId = Guid.NewGuid();
        foreach (var name in new[] { "First", "Second" })
        {
            var group = new FantasyGroup { Name = name };
            database.Leagues.Add(new() { Group = group, LeagueId = leagueId, Name = "League" });
            database.Baskets.Add(new() { Group = group, BasketId = basketId, Name = "Basket" });
        }
        await database.SaveChangesAsync();
        Assert.Equal(2, await database.Leagues.CountAsync(x => x.LeagueId == leagueId));
        Assert.Equal(2, await database.Baskets.CountAsync(x => x.BasketId == basketId));
    }

    [PostgreSqlFact]
    public async Task Two_leagues_share_one_basket_and_one_player_season()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var group = new FantasyGroup { Name = "Friends" };
        var season = new Season { SeasonIndex = 15, StartYear = 2026 };
        var basket = new BasketSeason
        {
            Basket = new() { Group = group, BasketId = Guid.NewGuid(), Name = "Shared basket" },
            Season = season
        };
        foreach (var name in new[] { "League", "Cup" })
        {
            database.LeagueBasketSeasons.Add(new()
            {
                BasketSeason = basket,
                LeagueSeason = new()
                {
                    League = new() { Group = group, LeagueId = Guid.NewGuid(), Name = name },
                    Season = season
                }
            });
        }
        database.PlayerSeasons.Add(new()
        {
            Player = new() { Name = "Shared player" }, Season = season,
            Position = PlayerPosition.Forward, IsActive = true
        });
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        Assert.Equal(2, await database.LeagueBasketSeasons.CountAsync());
        Assert.Equal(1, await database.BasketSeasons.CountAsync());
        Assert.Equal(1, await database.PlayerSeasons.CountAsync());
        Assert.Equal(1, await database.Players.CountAsync());
        var leagues = await database.LeagueSeasons.Include(x => x.Baskets).ThenInclude(x => x.BasketSeason).ToListAsync();
        Assert.Same(leagues[0].Baskets.Single().BasketSeason, leagues[1].Baskets.Single().BasketSeason);
    }

    [PostgreSqlFact]
    public async Task Composite_foreign_key_rejects_a_basket_from_another_group()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var data = await SeedCompetitionAsync(database);
        var otherGroup = new FantasyGroup { Name = "Other group" };
        var otherBasket = new GroupBasket { Group = otherGroup, BasketId = Guid.NewGuid(), Name = "Other basket" };
        database.BasketSeasons.Add(new() { Basket = otherBasket, SeasonIndex = 15 });
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        database.LeagueBasketSeasons.Add(new()
        {
            GroupId = data.GroupId, LeagueId = data.LeagueId,
            BasketId = otherBasket.BasketId, SeasonIndex = 15
        });
        await AssertSqlStateAsync(database, PostgresErrorCodes.ForeignKeyViolation);
    }

    [PostgreSqlFact]
    public async Task Composite_foreign_key_rejects_a_basket_from_another_season()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var data = await SeedCompetitionAsync(database);
        database.Seasons.Add(new() { SeasonIndex = 14, StartYear = 2025 });
        var otherBasket = new GroupBasket { GroupId = data.GroupId, BasketId = Guid.NewGuid(), Name = "Historical basket" };
        database.BasketSeasons.Add(new() { Basket = otherBasket, SeasonIndex = 14 });
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        database.LeagueBasketSeasons.Add(new()
        {
            GroupId = data.GroupId, LeagueId = data.LeagueId,
            BasketId = otherBasket.BasketId, SeasonIndex = 15
        });
        await AssertSqlStateAsync(database, PostgresErrorCodes.ForeignKeyViolation);
    }

    [PostgreSqlFact]
    public async Task Duplicate_player_season_is_rejected_without_deduplicating_names()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var season = new Season { SeasonIndex = 15, StartYear = 2026 };
        var first = new PlayerSeason { Player = new() { Name = "Same name" }, Season = season };
        database.PlayerSeasons.AddRange(first, new() { Player = new() { Name = "Same name" }, Season = season });
        await database.SaveChangesAsync();
        Assert.Equal(2, await database.Players.CountAsync());
        database.ChangeTracker.Clear();
        database.PlayerSeasons.Add(new() { PlayerId = first.PlayerId, SeasonIndex = 15 });
        await AssertSqlStateAsync(database, PostgresErrorCodes.UniqueViolation);
    }

    [PostgreSqlFact]
    public async Task Email_must_be_canonical_and_unique()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        database.Users.Add(new() { Email = "friend@example.test", DisplayName = "Friend" });
        await database.SaveChangesAsync();
        database.Users.Add(new() { Email = "friend@example.test", DisplayName = "Duplicate" });
        await AssertSqlStateAsync(database, PostgresErrorCodes.UniqueViolation);
    }

    [PostgreSqlFact]
    public async Task Noncanonical_email_is_rejected_instead_of_silently_merging_accounts()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        database.Users.Add(new() { Email = " Friend@Example.test ", DisplayName = "Friend" });
        await AssertSqlStateAsync(database, PostgresErrorCodes.CheckViolation);
    }

    [PostgreSqlFact]
    public async Task Deleting_a_group_does_not_silently_delete_its_competitions()
    {
        await using var database = fixture.CreateContext();
        await using var transaction = await database.Database.BeginTransactionAsync();
        var data = await SeedCompetitionAsync(database);
        var exception = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM \"Groups\" WHERE \"Id\" = {data.GroupId}"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
    }

    private static async Task<(Guid GroupId, Guid LeagueId)> SeedCompetitionAsync(FantasoccerDbContext database)
    {
        var group = new FantasyGroup { Name = "Group" };
        var league = new GroupLeague { Group = group, LeagueId = Guid.NewGuid(), Name = "League" };
        database.LeagueSeasons.Add(new()
        {
            League = league, Season = new() { SeasonIndex = 15, StartYear = 2026 }
        });
        await database.SaveChangesAsync();
        return (group.Id, league.LeagueId);
    }

    private static async Task AssertSqlStateAsync(FantasoccerDbContext database, string sqlState)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => database.SaveChangesAsync());
        Assert.Equal(sqlState, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }
}
