using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Fantasoccer.Infrastructure.PostgreSql;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Season_key_migration_reuses_recorded_keys_and_preserves_player_traces_and_vote_dependencies()
    {
        await using var database = fixture.CreateContext();
        var migrator = database.GetService<IMigrator>(); await migrator.MigrateAsync("20261026194948_UnresolvedPlayerCatalogEntries");
        database.Seasons.Add(new() { SeasonIndex = 15, StartYear = 2026 }); await database.SaveChangesAsync();
        var person = Guid.NewGuid(); var other = Guid.NewGuid(); var club = Guid.NewGuid();
        await LegacyPlayerAsync(database, person, "Display name"); await LegacyPlayerAsync(database, other, "Fallback. Defender");
        await database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Clubs\" (\"Id\",\"Name\",\"Abbreviation\") VALUES ({club}, 'Migration club', 'mig')");
        foreach (var id in new[] { person, other }) await LegacyPlayerSeasonAsync(database, id);
        database.LegacyIdentityMaps.Add(new() { SourceId = "verified-fixture", Kind = "player", LegacyKey = "recordedname", TargetId = person });
        await database.SaveChangesAsync();
        await LegacyVoteAsync(database, person, club);
        await migrator.MigrateAsync();
        Assert.Equal("recordedname", (await database.PlayerSeasons.AsNoTracking().SingleAsync(x => x.PlayerId == "recordedname")).PlayerKey);
        Assert.Equal("fallbackdefender", (await database.PlayerSeasons.AsNoTracking().SingleAsync(x => x.PlayerId == "fallbackdefender")).PlayerKey);
        Assert.Equal("recordedname", (await database.FootballVotes.SingleAsync()).PlayerId);
        Assert.Equal(person, (await database.Players.SingleAsync(x => x.Id == "recordedname")).LegacyId);
        Assert.Equal(other, (await database.Players.SingleAsync(x => x.Id == "fallbackdefender")).LegacyId);
        Assert.Equal(2, await database.Players.CountAsync());
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20261026194948_UnresolvedPlayerCatalogEntries"));
        Assert.Equal("recordedname", (await database.PlayerSeasons.AsNoTracking().SingleAsync(x => x.PlayerId == "recordedname")).PlayerKey);
    }

    [CoreFact]
    public async Task Season_key_migration_refuses_collisions_without_merging_or_deleting_existing_IDs()
    {
        await using var database = fixture.CreateContext();
        var migrator = database.GetService<IMigrator>(); await migrator.MigrateAsync("20261026194948_UnresolvedPlayerCatalogEntries");
        database.Seasons.Add(new() { SeasonIndex = 15, StartYear = 2026 }); await database.SaveChangesAsync();
        var first = Guid.NewGuid(); var second = Guid.NewGuid();
        await LegacyPlayerAsync(database, first, "Collision player"); await LegacyPlayerAsync(database, second, "COLLISION-PLAYER");
        await LegacyPlayerSeasonAsync(database, first); await LegacyPlayerSeasonAsync(database, second);
        Assert.Contains("unique seasonal player keys", (await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync())).MessageText);
        Assert.Equal(new[] { first, second }.Order(), (await database.Database.SqlQueryRaw<Guid>("SELECT \"Id\" AS \"Value\" FROM \"Players\"").ToArrayAsync()).Order());
        Assert.Equal(2, await database.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"PlayerSeasons\"").SingleAsync());
        Assert.DoesNotContain("20261026202604_SeasonPlayerKeys", await database.Database.GetAppliedMigrationsAsync());
    }

    [CoreFact]
    public async Task Season_key_migration_refuses_unverifiable_non_ASCII_names_without_discarding_the_original_data()
    {
        await using var database = fixture.CreateContext();
        var migrator = database.GetService<IMigrator>(); await migrator.MigrateAsync("20261026194948_UnresolvedPlayerCatalogEntries");
        database.Seasons.Add(new() { SeasonIndex = 15, StartYear = 2026 }); await database.SaveChangesAsync();
        var id = Guid.NewGuid(); const string name = "Fïxture player";
        await LegacyPlayerAsync(database, id, name); await LegacyPlayerSeasonAsync(database, id);
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
        Assert.Contains("verifiable normalization", refusal.MessageText); Assert.DoesNotContain(name, refusal.MessageText);
        Assert.Equal(name, await database.Players.Select(x => x.Name).SingleAsync());
        Assert.Equal(id, await database.Database.SqlQueryRaw<Guid>("SELECT \"Id\" AS \"Value\" FROM \"Players\"").SingleAsync());
    }

    [CoreFact]
    public async Task Season_key_migration_refuses_ambiguous_recorded_keys_instead_of_guessing_a_name()
    {
        await using var database = fixture.CreateContext();
        var migrator = database.GetService<IMigrator>(); await migrator.MigrateAsync("20261026194948_UnresolvedPlayerCatalogEntries");
        database.Seasons.Add(new() { SeasonIndex = 15, StartYear = 2026 }); await database.SaveChangesAsync();
        var id = Guid.NewGuid(); await LegacyPlayerAsync(database, id, "Display only"); await LegacyPlayerSeasonAsync(database, id);
        foreach (var key in new[] { "firstkey", "otherkey" }) database.LegacyIdentityMaps.Add(new() { SourceId = "fixture", Kind = "player", LegacyKey = key, TargetId = id });
        await database.SaveChangesAsync();
        Assert.Contains("ambiguous legacy player keys", (await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync())).MessageText);
        Assert.Equal(id, await database.Database.SqlQueryRaw<Guid>("SELECT \"Id\" AS \"Value\" FROM \"Players\"").SingleAsync());
    }

    private static Task<int> LegacyPlayerAsync(FantasoccerDbContext database, Guid id, string name) =>
        database.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"Players\" (\"Id\",\"Name\") VALUES ({id},{name})");
    private static Task<int> LegacyPlayerSeasonAsync(FantasoccerDbContext database, Guid id) => database.Database.ExecuteSqlInterpolatedAsync($"""
        INSERT INTO "PlayerSeasons" ("PlayerId","SeasonIndex","Position","IsActive","IsVisible") VALUES ({id},15,3,true,true)
        """);
    private static Task<int> LegacyVoteAsync(FantasoccerDbContext database, Guid player, Guid club)
    {
        // Seed the historical UUID schema, never the current natural-key model.
        var fields = typeof(FootballVote).GetProperties().Where(p => p.Name is not ("Player" or "Club")).ToArray();
        var row = new FootballVote { SeasonIndex = 15, Day = 1, Source = FootballVoteSource.Official, Value = 7 };
        var values = fields.Select(p => p.Name == "PlayerId" ? (object)player : p.Name == "ClubId" ? club :
            p.PropertyType.IsEnum ? Convert.ToInt32(p.GetValue(row)) : p.GetValue(row)!).ToArray();
        var sql = "INSERT INTO \"FootballVotes\" (" + string.Join(',', fields.Select(p => "\"" + p.Name + "\"")) + ") VALUES (" +
            string.Join(',', fields.Select((_, i) => "{" + i + "}")) + ")";
        return database.Database.ExecuteSqlRawAsync(sql, values);
    }
}
