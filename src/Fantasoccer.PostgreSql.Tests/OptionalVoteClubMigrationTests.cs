using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Optional_vote_club_upgrade_preserves_known_codes_and_values_and_unknown_snapshots_refuse_lossy_downgrade()
    {
        await using var database = fixture.CreateContext(); var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261027171151_ClubSeasonsAndCanonicalNames");
        var club = new FootballClub { Name = "Juve" }; var person = new FootballPlayer { Name = "Optional migration keeper" };
        database.PlayerSeasons.Add(new() { Player = person, Club = club, Season = new() { SeasonIndex = 12, StartYear = 2023 }, Position = PlayerPosition.Goalkeeper });
        database.FootballVotes.Add(new() { Player = person, Club = club, SeasonIndex = 12, Day = 10, Source = FootballVoteSource.Live, Value = 6, HasVote = true, IsActive = false });
        database.PushDayCheckpoints.Add(new() { SeasonIndex = 12, Day = 10, HasMatchEventVotes = true, Votes =
            [new() { PlayerId = person.Id, ClubId = club.Id, Value = 6, HasVote = true }] });
        await database.SaveChangesAsync(); database.ChangeTracker.Clear();
        await migrator.MigrateAsync();
        var vote = await database.FootballVotes.SingleAsync(); Assert.Equal("juv", vote.ClubId); Assert.Equal(6, vote.Value); Assert.True(vote.HasVote);
        var checkpoint = await database.PushDayCheckpoints.AsSplitQuery().SingleAsync(); Assert.Equal("juv", checkpoint.Votes.Single().ClubId);
        vote.ClubId = null; checkpoint.Votes.Single().ClubId = null; await database.SaveChangesAsync();
        Assert.Equal("juv", (await database.PlayerSeasons.SingleAsync()).ClubId);
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20261027171151_ClubSeasonsAndCanonicalNames"));
        Assert.Contains("unknown historical clubs", refusal.MessageText);
        database.ChangeTracker.Clear(); Assert.Null((await database.FootballVotes.SingleAsync()).ClubId);
        Assert.Null((await database.PushDayCheckpoints.AsSplitQuery().SingleAsync()).Votes.Single().ClubId);
        Assert.Equal(6, (await database.FootballVotes.SingleAsync()).Value);
        await using var transaction = await database.Database.BeginTransactionAsync();
        var failure = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlRawAsync("UPDATE \"FootballVotes\" SET \"ClubCode\"='xyz'"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
    }
}
