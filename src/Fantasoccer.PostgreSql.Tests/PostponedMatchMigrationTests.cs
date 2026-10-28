using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    private const string BeforePostponements = "20261027214331_CurrentOnlyFantasyStandings";

    [CoreFact]
    public async Task Postponement_migration_remembers_existing_delays_and_refuses_a_downgrade_that_would_forget_a_recovery()
    {
        await using var database = fixture.CreateContext(); var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync(BeforePostponements);
        database.Seasons.Add(new() { SeasonIndex = 10, StartYear = 2021 }); await database.SaveChangesAsync();
        var kickoff = new DateTimeOffset(2022, 3, 1, 14, 0, 0, TimeSpan.Zero);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Clubs" ("ClubCode","Name","Abbreviation") VALUES ('cag','Cagliari','cag'),('nap','Napoli','nap'),('mil','Milan','mil'),('int','Inter','int');
            INSERT INTO "FootballMatchdays" ("SeasonIndex","SerieADay") VALUES (10, 26);
            INSERT INTO "FootballMatches" ("SeasonIndex","SerieADay","Ordinal","HomeClubCode","AwayClubCode","KickoffUtc","Delayed") VALUES
                (10, 26, 0, 'cag', 'nap', {kickoff}, true), (10, 26, 1, 'mil', 'int', {kickoff}, false);
            """);
        await migrator.MigrateAsync();
        var matches = await database.FootballMatches.AsNoTracking().OrderBy(x => x.Ordinal).ToArrayAsync();
        Assert.True(matches[0].WasPostponed); Assert.True(matches[0].Delayed); Assert.False(matches[0].ActualOfficialVotesReady);
        Assert.Equal(kickoff, matches[0].OriginalKickoffUtc); Assert.Null(matches[0].IsFinished);
        Assert.False(matches[1].WasPostponed); Assert.Null(matches[1].ActualOfficialVotesReady); Assert.Equal(kickoff, matches[1].OriginalKickoffUtc);

        // The recovery was played: the old schema could only forget the postponement.
        var recovery = kickoff.AddDays(40);
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "FootballMatches" SET "Delayed" = false, "KickoffUtc" = {recovery}, "IsFinished" = true, "ActualOfficialVotesReady" = true WHERE "Ordinal" = 0
            """);
        var refused = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync(BeforePostponements));
        Assert.Contains("Cannot downgrade postponement policies", refused.MessageText);
        Assert.Contains("20261028112843_PostponedMatchPoliciesAndLegacyVotes", await database.Database.GetAppliedMigrationsAsync());
        var kept = await database.FootballMatches.AsNoTracking().SingleAsync(x => x.Ordinal == 0);
        Assert.True(kept.WasPostponed); Assert.Equal(kickoff, kept.OriginalKickoffUtc); Assert.Equal(recovery, kept.KickoffUtc);

        // Nothing beyond the old Delayed flag: the downgrade is lossless and allowed.
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "FootballMatches" SET "Delayed" = true, "KickoffUtc" = {kickoff}, "IsFinished" = NULL, "ActualOfficialVotesReady" = false WHERE "Ordinal" = 0
            """);
        await migrator.MigrateAsync(BeforePostponements);
        Assert.Equal(new[] { true, false }, await database.Database.SqlQueryRaw<bool>("SELECT \"Delayed\" AS \"Value\" FROM \"FootballMatches\" ORDER BY \"Ordinal\"").ToArrayAsync());
        await migrator.MigrateAsync();
        Assert.True((await database.FootballMatches.AsNoTracking().SingleAsync(x => x.Ordinal == 0)).WasPostponed);
    }
}
