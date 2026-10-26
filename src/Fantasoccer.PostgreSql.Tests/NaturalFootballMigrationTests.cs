using System.Data;
using Fantasoccer.Infrastructure.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Natural_key_migration_converts_populated_rosters_votes_and_owned_hall_records_without_losing_finances_or_UUID_traces()
    {
        await ImportAsync(); // Core groups/users/annual teams; no football identities yet.
        await using var database = fixture.CreateContext();
        var migrator = database.GetService<IMigrator>(); await migrator.MigrateAsync("20261026202604_SeasonPlayerKeys");
        var team = await database.TeamSeasons.Select(x => new { x.Id, x.GroupId, x.BasketId, x.SeasonIndex }).FirstAsync();
        var owner = await database.TeamOwnerships.Where(x => x.TeamId == team.Id && x.IsPrimary).Select(x => x.UserId).SingleAsync();
        var league = await database.Leagues.Where(x => x.GroupId == team.GroupId).Select(x => x.LeagueId).FirstAsync();
        var player = Guid.NewGuid(); var club = Guid.NewGuid();
        await LegacyPlayerAsync(database, player, "Migration forward");
        await database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Clubs\" (\"Id\",\"Name\",\"Abbreviation\") VALUES ({club}, 'Fixture Napoli', 'nap')");
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PlayerSeasons" ("PlayerId","SeasonIndex","NormalizedName","ClubId","Position","IsActive","IsVisible")
            VALUES ({player},15,'migrationforward',{club},3,true,true)
            """);
        await LegacyVoteAsync(database, player, club);
        var scope = new Dictionary<string, object> { ["GroupId"] = team.GroupId, ["BasketId"] = team.BasketId,
            ["SeasonIndex"] = 15, ["TeamId"] = team.Id, ["Kind"] = 0, ["Day"] = 0, ["CompetitionId"] = Guid.Empty };
        await InsertHistoricalRowAsync(database, "RosterSnapshots", new(scope) { ["TeamOriginYear"] = 15, ["OwnerUserId"] = owner,
            ["Name"] = "Frozen team", ["MoneyFromRank"] = 19, ["MarketBalanceAdjustment"] = -7, ["OpeningCompetitionPrize"] = 4, ["Version"] = 3 });
        await InsertHistoricalRowAsync(database, "RosterPlayers", new(scope) { ["Ordinal"] = 0, ["PlayerId"] = player, ["ClubId"] = club,
            ["PlayerRole"] = 3, ["Price"] = 42, ["Revenue"] = 58, ["Status"] = 3, ["Position"] = 11 });
        await InsertHistoricalRowAsync(database, "HallArchives", new() { ["GroupId"] = team.GroupId, ["LeagueId"] = league });
        await InsertHistoricalRowAsync(database, "HallPlayerRecords", new() { ["GroupId"] = team.GroupId, ["LeagueId"] = league,
            ["Kind"] = 0, ["Points"] = 17.5f, ["Player_PlayerId"] = player, ["Player_ClubId"] = club,
            ["Player_Name"] = "Frozen player label", ["Player_Price"] = 55, ["Player_Revenue"] = 12 });
        await migrator.MigrateAsync();
        var saved = await database.Players.AsNoTracking().SingleAsync(); Assert.Equal("migrationforward", saved.Id); Assert.Equal(player, saved.LegacyId);
        var savedClub = await database.Clubs.AsNoTracking().SingleAsync(); Assert.Equal("nap", savedClub.Id); Assert.Equal(club, savedClub.LegacyId);
        var roster = await database.RosterSnapshots.AsNoTracking().AsSplitQuery().SingleAsync();
        Assert.Equal(19, roster.MoneyFromRank); Assert.Equal(-7, roster.MarketBalanceAdjustment); Assert.Equal(4, roster.OpeningCompetitionPrize); Assert.Equal(3, roster.Version);
        var row = Assert.Single(roster.Players); Assert.Equal("migrationforward", row.PlayerId); Assert.Equal("nap", row.ClubId);
        Assert.Equal(42, row.Price); Assert.Equal(58, row.Revenue); Assert.Equal(3, row.Status); Assert.Equal(11, row.Position);
        var vote = await database.FootballVotes.AsNoTracking().SingleAsync(); Assert.Equal("migrationforward", vote.PlayerId); Assert.Equal("nap", vote.ClubId); Assert.Equal(7, vote.Value);
        var hall = (await database.HallArchives.AsNoTracking().AsSplitQuery().SingleAsync()).PlayerRecords.Single();
        Assert.Equal("migrationforward", hall.Player.PlayerId); Assert.Equal("nap", hall.Player.ClubId); Assert.Equal(55, hall.Player.Price); Assert.Equal(12, hall.Player.Revenue);
        Assert.Equal("Frozen player label", hall.Player.Name); Assert.Equal(17.5f, hall.Points);
        Assert.Empty(await database.Database.SqlQueryRaw<string>("""
            SELECT c.conname AS "Value" FROM pg_constraint c JOIN pg_attribute a ON a.attrelid=c.conrelid AND a.attnum=ANY(c.conkey)
            WHERE c.contype='f' AND c.confrelid IN ('"Players"'::regclass,'"Clubs"'::regclass,'"PlayerSeasons"'::regclass) AND a.atttypid='uuid'::regtype
            """).ToArrayAsync());
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20261026202604_SeasonPlayerKeys"));
        Assert.Equal("migrationforward", (await database.Players.AsNoTracking().SingleAsync()).Id);
    }

    [CoreFact]
    public async Task Natural_key_migration_refuses_duplicate_global_players_even_if_their_old_years_differ()
    {
        await using var database = fixture.CreateContext(); var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261026202604_SeasonPlayerKeys");
        database.Seasons.AddRange(new() { SeasonIndex = 15, StartYear = 2026 }, new() { SeasonIndex = 16, StartYear = 2027 }); await database.SaveChangesAsync();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await LegacyPlayerAsync(database, first, "Duplicate fixture"); await LegacyPlayerAsync(database, second, "DUPLICATE FIXTURE");
        foreach (var (id, year) in new[] { (first, 15), (second, 16) })
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PlayerSeasons" ("PlayerId","SeasonIndex","NormalizedName","Position","IsActive","IsVisible")
                VALUES ({id},{year},'duplicatefixture',3,true,true)
                """);
        Assert.Contains("duplicated", (await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync())).MessageText);
        Assert.Equal(new[] { first, second }.Order(), (await database.Database.SqlQueryRaw<Guid>("SELECT \"Id\" AS \"Value\" FROM \"Players\"").ToArrayAsync()).Order());
    }

    [CoreFact]
    public async Task Natural_key_migration_refuses_duplicate_or_missing_club_codes_without_guessing_from_display_names()
    {
        await using var database = fixture.CreateContext(); var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261026202604_SeasonPlayerKeys");
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Clubs\" (\"Id\",\"Name\",\"Abbreviation\") VALUES ({first},'First fixture','dup'),({second},'Second fixture','DUP')");
        Assert.Contains("Club abbreviations", (await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync())).MessageText);
        Assert.Equal(2, await database.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"Clubs\"").SingleAsync());
        await database.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Clubs\" SET \"Abbreviation\"='' WHERE \"Id\"={second}");
        Assert.Contains("Club abbreviations", (await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync())).MessageText);
        Assert.Equal(new[] { first, second }.Order(), (await database.Database.SqlQueryRaw<Guid>("SELECT \"Id\" AS \"Value\" FROM \"Clubs\"").ToArrayAsync()).Order());
        Assert.DoesNotContain("20261026214941_NaturalFootballKeys", await database.Database.GetAppliedMigrationsAsync());
    }

    private static async Task InsertHistoricalRowAsync(FantasoccerDbContext database, string table, Dictionary<string, object> supplied)
    {
        // Seed an old physical schema without asking the current EF model to
        // insert obsolete UUID columns. Every table/override is synthetic.
        if (database.Database.GetDbConnection().State != ConnectionState.Open) await database.Database.OpenConnectionAsync();
        await using var read = database.Database.GetDbConnection().CreateCommand();
        read.CommandText = "SELECT column_name,data_type,is_nullable,column_default FROM information_schema.columns WHERE table_schema='public' AND table_name=@table ORDER BY ordinal_position";
        var parameter = read.CreateParameter(); parameter.ParameterName = "table"; parameter.Value = table; read.Parameters.Add(parameter);
        var fields = new List<(string Name, object Value)>();
        await using (var reader = await read.ExecuteReaderAsync())
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(0);
                if (supplied.TryGetValue(name, out var value)) { fields.Add((name, value)); continue; }
                if (reader.GetString(2) == "YES" || !reader.IsDBNull(3)) continue;
                object fallback = reader.GetString(1) switch
                {
                    "uuid" => Guid.NewGuid(), "integer" => 0, "bigint" => 0L, "real" => 0f, "double precision" => 0d,
                    "boolean" => false, "numeric" => 0m, "timestamp with time zone" => DateTimeOffset.UnixEpoch,
                    "timestamp without time zone" => DateTime.SpecifyKind(DateTime.UnixEpoch, DateTimeKind.Unspecified), _ => ""
                };
                fields.Add((name, fallback));
            }
        var sql = "INSERT INTO \"" + table + "\" (" + string.Join(',', fields.Select(x => "\"" + x.Name + "\"")) + ") VALUES (" +
            string.Join(',', fields.Select((_, i) => "{" + i + "}")) + ")";
        await database.Database.ExecuteSqlRawAsync(sql, fields.Select(x => x.Value));
    }
}
