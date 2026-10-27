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
    public async Task Club_season_migration_normalizes_existing_names_and_backfills_only_real_match_and_rank_evidence()
    {
        await using var database = fixture.CreateContext(); var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261026214941_NaturalFootballKeys");
        database.Seasons.AddRange(new() { SeasonIndex = 15, StartYear = 2026 }, new() { SeasonIndex = 16, StartYear = 2027 });
        await database.SaveChangesAsync();
        var legacy = Guid.NewGuid();
        await database.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Clubs" ("ClubCode","Name","Abbreviation","LegacyId") VALUES
                ('juv','JUVE','JUV',{legacy}), ('cat','catania','CAT',NULL), ('xyz','Historical external club','XYZ',NULL)
            """);
        database.FootballMatchdays.Add(new() { SeasonIndex = 15, SerieADay = 1, Matches =
            [new() { SeasonIndex = 15, SerieADay = 1, Ordinal = 0, HomeClubId = "juv", AwayClubId = "cat" }] });
        database.FootballClubRanks.AddRange(new() { SeasonIndex = 15, ClubId = "cat", Points = 3 }, new() { SeasonIndex = 16, ClubId = "juv", Points = 4 });
        database.FootballDatasets.Add(new() { Kind = "realteamwrapper", SeasonIndex = 16 }); // Old marker cannot reconstruct the old list.
        database.PlayerSeasons.AddRange(new() { Player = new() { Name = "Historical migration player" }, SeasonIndex = 16, ClubId = "cat", Position = PlayerPosition.Forward },
            new() { Player = new() { Name = "External migration player" }, SeasonIndex = 15, ClubId = "xyz", Position = PlayerPosition.Forward });
        await database.SaveChangesAsync(); database.ChangeTracker.Clear();
        await migrator.MigrateAsync();
        var club = await database.Clubs.SingleAsync(x => x.Id == "juv"); Assert.Equal("Juventus", club.Name); Assert.Equal("juv", club.Abbreviation); Assert.Equal(legacy, club.LegacyId);
        Assert.Equal("Catania", (await database.Clubs.SingleAsync(x => x.Id == "cat")).Name);
        Assert.Equal("Historical external club", (await database.Clubs.SingleAsync(x => x.Id == "xyz")).Name);
        Assert.Equal(3, await database.ClubSeasons.CountAsync());
        Assert.Equal(ClubMembershipSource.Calendar, (await database.ClubSeasons.SingleAsync(x => x.ClubId == "juv" && x.SeasonIndex == 15)).Sources);
        Assert.Equal(ClubMembershipSource.Calendar | ClubMembershipSource.Standings,
            (await database.ClubSeasons.SingleAsync(x => x.ClubId == "cat" && x.SeasonIndex == 15)).Sources);
        Assert.Equal(ClubMembershipSource.Standings, (await database.ClubSeasons.SingleAsync(x => x.ClubId == "juv" && x.SeasonIndex == 16)).Sources);
        Assert.False(await database.ClubSeasons.AnyAsync(x => x.ClubId == "xyz" || x.ClubId == "cat" && x.SeasonIndex == 16));
        Assert.False(await database.FootballDatasets.AnyAsync(x => x.Kind == FootballClubSeason.CatalogueDatasetKind));
        Assert.Equal("cat", (await database.PlayerSeasons.SingleAsync(x => x.SeasonIndex == 16)).ClubId);
        var match = await database.FootballMatches.SingleAsync(); Assert.Equal("juv", match.HomeClubId); Assert.Equal("cat", match.AwayClubId);
        Assert.Contains("Cannot downgrade populated canonical clubs", (await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync("20261026214941_NaturalFootballKeys"))).MessageText);
        Assert.Equal(3, await database.ClubSeasons.CountAsync()); Assert.Equal("Juventus", (await database.Clubs.AsNoTracking().SingleAsync(x => x.Id == "juv")).Name);
    }

    [CoreFact]
    public async Task Club_name_migration_refuses_existing_conflicts_without_relabeling_or_partially_creating_memberships()
    {
        await using var database = fixture.CreateContext(); var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("20261026214941_NaturalFootballKeys");
        await database.Database.ExecuteSqlRawAsync("INSERT INTO \"Clubs\" (\"ClubCode\",\"Name\",\"Abbreviation\") VALUES ('juv','Napoli','juv')");
        foreach (var (name, abbreviation) in new[] { ("Napoli", "juv"), ("Juve", "NAP") })
        {
            await database.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Clubs\" SET \"Name\"={name},\"Abbreviation\"={abbreviation} WHERE \"ClubCode\"='juv'");
            var failure = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
            Assert.Contains("conflicting club", failure.MessageText);
            Assert.Equal(name, await database.Clubs.Select(x => x.Name).SingleAsync());
            Assert.Equal(abbreviation, await database.Clubs.Select(x => x.Abbreviation).SingleAsync());
            Assert.DoesNotContain("20261027171151_ClubSeasonsAndCanonicalNames", await database.Database.GetAppliedMigrationsAsync());
            Assert.Equal(0, await database.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM information_schema.tables WHERE table_schema='public' AND table_name='ClubSeasons'").SingleAsync());
        }
    }

    [CoreFact]
    public async Task The_canonical_club_and_season_schema_can_be_downgraded_and_recreated_when_the_graph_is_empty()
    {
        await using var database = fixture.CreateContext(); var migrator = database.GetService<IMigrator>();
        await migrator.MigrateAsync("0"); Assert.Empty(await database.Database.GetAppliedMigrationsAsync());
        await migrator.MigrateAsync(); Assert.Empty(await database.Clubs.ToArrayAsync()); Assert.Empty(await database.ClubSeasons.ToArrayAsync());
    }
}
