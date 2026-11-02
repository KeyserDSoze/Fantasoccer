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
        database.Seasons.Add(new() { SeasonIndex = 12, StartYear = 2023 }); database.Clubs.Add(club); database.Players.Add(person);
        await database.SaveChangesAsync();
        // PlayerSeasons gained columns later: write it with this schema's SQL.
        await database.Database.ExecuteSqlRawAsync(
            "INSERT INTO \"PlayerSeasons\" (\"PlayerKey\", \"SeasonIndex\", \"ClubCode\", \"Position\", \"IsActive\", \"IsVisible\") VALUES ({0}, 12, {1}, 0, false, true)", person.Id, club.Id);
        database.FootballVotes.Add(new() { PlayerId = person.Id, ClubId = club.Id, SeasonIndex = 12, Day = 10, Source = FootballVoteSource.Live, Value = 6, HasVote = true, IsActive = false });
        await database.SaveChangesAsync(); database.ChangeTracker.Clear();
        // Written with the columns of that historical schema (later migrations add more).
        await database.Database.ExecuteSqlRawAsync("""
            INSERT INTO "PushDayCheckpoints" ("SeasonIndex", "Day", "DeploymentReminder", "EndDay", "LiveChangesReminder", "HasMatchEventVotes", "MatchEventSequence")
            VALUES (12, 10, false, false, false, true, 0);
            INSERT INTO "PushVoteBaselines" ("SeasonIndex", "Day", "Ordinal", "PlayerKey", "ClubCode", "PlayerRole", "IsActive", "IsVisible", "VoteRole", "Value", "IsFinal",
                "Goals", "Penalties", "Assists", "StoppedPenalties", "SufferedGoals", "WrongedPenalties", "OwnGoals", "Status", "ManOfTheMatch", "HasVote", "IsOut", "IsIn", "Injured")
            VALUES (12, 10, 0, {0}, {1}, 0, false, false, 0, 6, false, 0, 0, 0, 0, 0, 0, 0, 0, false, true, false, false, false);
            """, person.Id, club.Id);
        await migrator.MigrateAsync();
        var vote = await database.FootballVotes.SingleAsync(); Assert.Equal("juv", vote.ClubId); Assert.Equal(6, vote.Value); Assert.True(vote.HasVote);
        var checkpoint = await database.PushDayCheckpoints.AsSplitQuery().SingleAsync(); Assert.Equal("juv", checkpoint.Votes.Single().ClubId);
        vote.ClubId = null; checkpoint.Votes.Single().ClubId = null; await database.SaveChangesAsync();
        Assert.Equal("juv", (await database.PlayerSeasons.SingleAsync()).ClubId);
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20261027171151_ClubSeasonsAndCanonicalNames"));
        Assert.Contains("unknown historical clubs", refusal.MessageText);
        database.ChangeTracker.Clear(); Assert.Null((await database.FootballVotes.SingleAsync()).ClubId);
        Assert.Null(await database.Database.SqlQueryRaw<string?>("SELECT \"ClubCode\" AS \"Value\" FROM \"PushVoteBaselines\"").SingleAsync());
        Assert.Equal(6, (await database.FootballVotes.SingleAsync()).Value);
        await using var transaction = await database.Database.BeginTransactionAsync();
        var failure = await Assert.ThrowsAsync<PostgresException>(() => database.Database.ExecuteSqlRawAsync("UPDATE \"FootballVotes\" SET \"ClubCode\"='xyz'"));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, failure.SqlState);
    }
}
