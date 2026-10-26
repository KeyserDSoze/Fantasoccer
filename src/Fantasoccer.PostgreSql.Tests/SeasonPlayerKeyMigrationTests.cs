using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Npgsql;
using Xunit;

namespace Fantasoccer.PostgreSql.Tests;

public sealed partial class CoreMigrationTests
{
    [CoreFact]
    public async Task Season_key_migration_reuses_recorded_keys_and_preserves_player_IDs_and_vote_dependencies()
    {
        await using var database = fixture.CreateContext();
        var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261026194948_UnresolvedPlayerCatalogEntries");
        database.Seasons.Add(new() { SeasonIndex = 15, StartYear = 2026 });
        var person = new FootballPlayer { Name = "Display name" }; var other = new FootballPlayer { Name = "Fallback. Defender" };
        var club = new FootballClub { Name = "Migration club" };
        database.Players.AddRange(person, other); database.Clubs.Add(club); await database.SaveChangesAsync();
        foreach (var id in new[] { person.Id, other.Id })
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PlayerSeasons" ("PlayerId", "SeasonIndex", "ClubId", "Position", "IsActive", "IsVisible")
                VALUES ({id}, 15, {club.Id}, 3, true, true)
                """);
        database.LegacyIdentityMaps.Add(new() { SourceId = "verified-fixture", Kind = "player", LegacyKey = "recordedname", TargetId = person.Id });
        database.FootballVotes.Add(new() { PlayerId = person.Id, ClubId = club.Id, SeasonIndex = 15, Day = 1, Source = FootballVoteSource.Official, Value = 7 });
        await database.SaveChangesAsync();
        await migrator.MigrateAsync();
        Assert.Equal("15:recordedname", (await database.PlayerSeasons.AsNoTracking().SingleAsync(x => x.PlayerId == person.Id)).PlayerKey);
        Assert.Equal("15:fallbackdefender", (await database.PlayerSeasons.AsNoTracking().SingleAsync(x => x.PlayerId == other.Id)).PlayerKey);
        Assert.Equal(person.Id, (await database.FootballVotes.SingleAsync()).PlayerId);
        Assert.Equal(2, await database.Players.CountAsync());
        await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20261026194948_UnresolvedPlayerCatalogEntries"));
        Assert.Equal("15:recordedname", (await database.PlayerSeasons.AsNoTracking().SingleAsync(x => x.PlayerId == person.Id)).PlayerKey);
    }

    [CoreFact]
    public async Task Season_key_migration_refuses_collisions_without_merging_or_deleting_existing_IDs()
    {
        await using var database = fixture.CreateContext();
        var migrator = database.GetService<IMigrator>(); await migrator.MigrateAsync("20261026194948_UnresolvedPlayerCatalogEntries");
        database.Seasons.Add(new() { SeasonIndex = 15, StartYear = 2026 });
        var players = new[] { new FootballPlayer { Name = "Collision player" }, new FootballPlayer { Name = "COLLISION-PLAYER" } };
        database.Players.AddRange(players); await database.SaveChangesAsync();
        foreach (var player in players)
            await database.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PlayerSeasons" ("PlayerId", "SeasonIndex", "Position", "IsActive", "IsVisible")
                VALUES ({player.Id}, 15, 3, true, true)
                """);
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
        Assert.Contains("unique seasonal player keys", refusal.MessageText);
        Assert.Equal(players.Select(x => x.Id).Order(), (await database.Players.AsNoTracking().Select(x => x.Id).ToArrayAsync()).Order());
        Assert.Equal(2, await database.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM \"PlayerSeasons\"").SingleAsync());
        Assert.DoesNotContain("20261026202604_SeasonPlayerKeys", await database.Database.GetAppliedMigrationsAsync());
    }

    [CoreFact]
    public async Task Season_key_migration_refuses_unverifiable_non_ASCII_names_without_discarding_the_original_data()
    {
        await using var database = fixture.CreateContext();
        var migrator = database.GetService<IMigrator>(); await migrator.MigrateAsync("20261026194948_UnresolvedPlayerCatalogEntries");
        database.Seasons.Add(new() { SeasonIndex = 15, StartYear = 2026 });
        var player = new FootballPlayer { Name = "Fïxture player" }; database.Players.Add(player); await database.SaveChangesAsync();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PlayerSeasons" ("PlayerId", "SeasonIndex", "Position", "IsActive", "IsVisible")
            VALUES ({player.Id}, 15, 3, true, true)
            """);
        var refusal = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
        Assert.Contains("verifiable normalization", refusal.MessageText); Assert.DoesNotContain(player.Name, refusal.MessageText);
        Assert.Equal(player.Name, (await database.Players.AsNoTracking().SingleAsync()).Name);
        Assert.Equal(player.Id, (await database.Players.AsNoTracking().SingleAsync()).Id);
    }

    [CoreFact]
    public async Task Season_key_migration_refuses_ambiguous_recorded_keys_instead_of_guessing_a_name()
    {
        await using var database = fixture.CreateContext();
        var migrator = database.GetService<IMigrator>(); await migrator.MigrateAsync("20261026194948_UnresolvedPlayerCatalogEntries");
        database.Seasons.Add(new() { SeasonIndex = 15, StartYear = 2026 });
        var player = new FootballPlayer { Name = "Display only" }; database.Players.Add(player); await database.SaveChangesAsync();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "PlayerSeasons" ("PlayerId", "SeasonIndex", "Position", "IsActive", "IsVisible")
            VALUES ({player.Id}, 15, 3, true, true)
            """);
        foreach (var key in new[] { "firstkey", "otherkey" })
            database.LegacyIdentityMaps.Add(new() { SourceId = "fixture", Kind = "player", LegacyKey = key, TargetId = player.Id });
        await database.SaveChangesAsync();
        Assert.Contains("ambiguous legacy player keys", (await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync())).MessageText);
        Assert.Equal(player.Id, (await database.Players.AsNoTracking().SingleAsync()).Id);
    }
}
