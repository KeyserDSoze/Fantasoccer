using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

// The revision is what lets a cached projection tell "same official votes" from
// "any vote changed". It is bumped by the database, so every writer counts.
public sealed class OfficialVoteRevisionTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private static async Task<long> RevisionAsync(FantasoccerDbContext database, int season, int day) =>
        await database.FootballDayRevisions.AsNoTracking().Where(x => x.SeasonIndex == season && x.Day == day).Select(x => x.OfficialRevision).SingleOrDefaultAsync();

    private static async Task<(FootballPlayer First, FootballPlayer Second)> SeedAsync(FantasoccerDbContext database, int season, string code)
    {
        var club = new FootballClub { Name = "Revision club " + code, Abbreviation = code };
        var first = new FootballPlayer { Name = "Revision first " + Guid.NewGuid().ToString("N") };
        var second = new FootballPlayer { Name = "Revision second " + Guid.NewGuid().ToString("N") };
        var year = new Season { SeasonIndex = season, StartYear = season + 2011 };
        database.PlayerSeasons.AddRange(new() { Player = first, Club = club, Season = year, Position = PlayerPosition.Forward },
            new() { Player = second, Club = club, Season = year, Position = PlayerPosition.Midfielder });
        await database.SaveChangesAsync();
        return (first, second);
    }

    private static FootballVote Vote(FootballPlayer player, int season, int day, FootballVoteSource source, float value = 6) =>
        new() { Player = player, SeasonIndex = season, Day = day, Source = source, Value = value, HasVote = true };

    [PostgreSqlFact]
    public async Task Any_change_of_an_official_vote_bumps_only_its_own_day_even_when_the_sum_of_the_votes_does_not_move()
    {
        await using var database = fixture.CreateContext(); await using var transaction = await database.Database.BeginTransactionAsync();
        var (first, second) = await SeedAsync(database, 90, "rva");
        Assert.Equal(0, await RevisionAsync(database, 90, 3));

        database.FootballVotes.AddRange(Vote(first, 90, 3, FootballVoteSource.Official), Vote(second, 90, 3, FootballVoteSource.Official));
        await database.SaveChangesAsync();
        var inserted = await RevisionAsync(database, 90, 3);
        Assert.True(inserted > 0);
        Assert.Equal(0, await RevisionAsync(database, 90, 4));

        // 6 -> 7 and 6 -> 5: the sum is identical, the revision moves.
        var rows = await database.FootballVotes.Where(x => x.SeasonIndex == 90 && x.Day == 3).OrderBy(x => x.PlayerId).ToArrayAsync();
        rows[0].Value = 7; rows[1].Value = 5;
        await database.SaveChangesAsync();
        var compensated = await RevisionAsync(database, 90, 3);
        Assert.True(compensated > inserted);

        // A column that is neither the vote nor goals/assists/status.
        rows[0].SufferedGoals = 1;
        await database.SaveChangesAsync();
        var conceded = await RevisionAsync(database, 90, 3);
        Assert.True(conceded > compensated);
        rows[0].ManOfTheMatch = true; rows[0].IsFinal = true;
        await database.SaveChangesAsync();
        Assert.True(await RevisionAsync(database, 90, 3) > conceded);

        // Saving without a change writes nothing.
        var steady = await RevisionAsync(database, 90, 3);
        await database.SaveChangesAsync();
        Assert.Equal(steady, await RevisionAsync(database, 90, 3));

        database.FootballVotes.Remove(rows[1]);
        await database.SaveChangesAsync();
        Assert.True(await RevisionAsync(database, 90, 3) > steady);
        Assert.Equal(0, await RevisionAsync(database, 90, 4));
    }

    [PostgreSqlFact]
    public async Task Raw_SQL_bulk_writes_count_and_only_official_votes_do()
    {
        await using var database = fixture.CreateContext(); await using var transaction = await database.Database.BeginTransactionAsync();
        var (first, second) = await SeedAsync(database, 91, "rvb");
        // Live votes and push baselines are not scoring inputs.
        database.FootballVotes.AddRange(Vote(first, 91, 5, FootballVoteSource.Live), Vote(second, 91, 5, FootballVoteSource.Notification));
        await database.SaveChangesAsync();
        Assert.Equal(0, await RevisionAsync(database, 91, 5));
        await database.Database.ExecuteSqlRawAsync("UPDATE \"FootballVotes\" SET \"Goals\" = 1 WHERE \"SeasonIndex\" = 91");
        Assert.Equal(0, await RevisionAsync(database, 91, 5));

        database.FootballVotes.Add(Vote(first, 91, 5, FootballVoteSource.Official));
        database.FootballVotes.Add(Vote(first, 91, 6, FootballVoteSource.Official));
        await database.SaveChangesAsync();
        var five = await RevisionAsync(database, 91, 5); var six = await RevisionAsync(database, 91, 6);
        Assert.True(five > 0 && six > 0);

        // One statement over several days bumps each of them; days it does not touch stay as they are.
        await database.Database.ExecuteSqlRawAsync("UPDATE \"FootballVotes\" SET \"Goals\" = 2 WHERE \"SeasonIndex\" = 91 AND \"Source\" = 2");
        Assert.True(await RevisionAsync(database, 91, 5) > five);
        Assert.True(await RevisionAsync(database, 91, 6) > six);
        var afterUpdate = await RevisionAsync(database, 91, 5);
        await database.Database.ExecuteSqlRawAsync("UPDATE \"FootballVotes\" SET \"Goals\" = 9 WHERE \"SeasonIndex\" = 91 AND \"Source\" = 0");
        Assert.Equal(afterUpdate, await RevisionAsync(database, 91, 5));
        await database.Database.ExecuteSqlRawAsync("DELETE FROM \"FootballVotes\" WHERE \"SeasonIndex\" = 91 AND \"Day\" = 6 AND \"Source\" = 2");
        Assert.True(await RevisionAsync(database, 91, 6) > six);
        Assert.Equal(afterUpdate, await RevisionAsync(database, 91, 5));
    }
}

// Own database: migrating down and up again must not disturb the other tests.
public sealed class OfficialVoteRevisionMigrationTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    private const string Previous = "20261030152653_SharedLineupsByDefault";

    [PostgreSqlFact]
    public async Task The_migration_starts_existing_official_days_at_one_and_can_be_reverted_cleanly()
    {
        await using var database = fixture.CreateContext(); var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        Assert.False(await TriggerExistsAsync(database));
        var club = new FootballClub { Name = "Migration revision club", Abbreviation = "mrv" };
        var player = new FootballPlayer { Name = "Migration revision player " + Guid.NewGuid().ToString("N") };
        database.PlayerSeasons.Add(new() { Player = player, Club = club, Season = new() { SeasonIndex = 92, StartYear = 2103 }, Position = PlayerPosition.Defender });
        database.FootballVotes.Add(new() { Player = player, SeasonIndex = 92, Day = 7, Source = FootballVoteSource.Official, Value = 6, HasVote = true });
        database.FootballVotes.Add(new() { Player = player, SeasonIndex = 92, Day = 8, Source = FootballVoteSource.Live, Value = 6, HasVote = true });
        await database.SaveChangesAsync(); database.ChangeTracker.Clear();

        await migrator.MigrateAsync();
        Assert.True(await TriggerExistsAsync(database));
        Assert.Equal(1, await database.FootballDayRevisions.Where(x => x.SeasonIndex == 92 && x.Day == 7).Select(x => x.OfficialRevision).SingleAsync());
        Assert.False(await database.FootballDayRevisions.AnyAsync(x => x.SeasonIndex == 92 && x.Day == 8));

        await migrator.MigrateAsync(Previous);
        Assert.False(await TriggerExistsAsync(database));
        Assert.Equal(2, await database.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"FootballVotes\" WHERE \"SeasonIndex\" = 92").SingleAsync());
        await migrator.MigrateAsync();
        Assert.True(await TriggerExistsAsync(database));
    }

    private static async Task<bool> TriggerExistsAsync(FantasoccerDbContext database) =>
        await database.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM pg_trigger WHERE tgname LIKE 'fz_official_votes_%' AND NOT tgisinternal").SingleAsync() == 3;
}
