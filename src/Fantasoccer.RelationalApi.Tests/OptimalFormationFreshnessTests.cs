using Fantasoccer.Application.Core.Caching;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.Caching;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class OptimalFormationFreshnessTests(PostgreSqlFixture fixture) : IClassFixture<PostgreSqlFixture>
{
    [PostgreSqlFact]
    public async Task Old_probabilities_stay_readable_but_the_algorithm_falls_back_to_persistent_statistics_until_reconfirmed()
    {
        const int year = 85;
        await using var database = fixture.CreateContext();
        var club = new FootballClub { Name = "Formation freshness club", Abbreviation = "ffc" };
        var star = new FootballPlayer { Name = "Freshness Star " + Guid.NewGuid().ToString("N") };
        var starter = new FootballPlayer { Name = "Freshness Starter " + Guid.NewGuid().ToString("N") };
        var season = new Season { SeasonIndex = year, StartYear = year + 2011 };
        foreach (var player in new[] { star, starter })
        {
            database.PlayerSeasons.Add(new() { Player = player, Club = club, Season = season, Position = PlayerPosition.Forward, IsActive = true });
            database.FootballPlayerStatistics.Add(new() { Player = player, Club = club, SeasonIndex = year, PlayerRole = 3, IsActive = true,
                Summatory = 70, FantaSummatory = 75, WithVote = 10 });
        }
        await database.SaveChangesAsync();
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var coordinator = new InMemoryCoordinatedDistributedCache(clock);
        using var memory = new MemoryCache(new MemoryCacheOptions());
        var cache = new SharedSnapshotCache(coordinator, memory, clock, Options.Create(new SharedCacheOptions()), NullLogger<SharedSnapshotCache>.Instance);
        var store = new FootballLiveDataStore(cache, clock);
        var payload = new FootballChancesPayload(year, 1, ["Fantagazzetta"],
        [
            new(star.Id, star.Name, club.Id, club.Name, 3, true, true, false, false, false, false, (int)ChanceType.Injury, "Old injury"),
            new(starter.Id, starter.Name, club.Id, club.Name, 3, true, true, true, false, false, false, (int)ChanceType.Normal, null)
        ]);
        await store.PublishChancesAsync(clock.GetUtcNow(), payload);
        var reader = new SqlFootballMatchdayReader(database, store, clock);
        var capture = new CaptureEngine();
        var registrations = new ServiceCollection();
        registrations.AddFantasoccerRuleEngines();
        registrations.AddSingleton<IFootballMatchdayReader>(reader);
        registrations.AddSingleton<IFormationStatisticsReader>(new SqlFormationStatisticsReader(database));
        registrations.AddSingleton<IOptimalFormationEngine>(capture);
        await using var services = registrations.BuildServiceProvider();
        var algorithm = services.GetRequiredService<IOptimalFormationService>();
        var team = new Team { Players = new[] { star, starter }.Select(player => new Player
        {
            Name = player.Name, Role = Role.Forward, RealTeam = new() { Name = club.Name, Abbreviation = club.Id },
            IsActive = true, Status = PlayerInTeamStatus.Active
        }).ToList() };
        var fresh = await algorithm.CalculateAsync(team, year, 1, LeagueType.League, LeagueSetting.Default);
        Assert.Equal(0, capture.Last!.Projections[star.Id].Availability);
        Assert.DoesNotContain(fresh.Warnings, x => x.Contains("non aggiornate"));

        clock.Advance(TimeSpan.FromHours(13));
        var input = await reader.ReadAsync(year, 1, true, true);
        Assert.NotNull(input.Chances); Assert.True(input.ChancesAreStale);
        Assert.NotNull(await store.ChancesAsync(year, 1)); // the 30-day consultation window was not shortened
        var fallback = await algorithm.CalculateAsync(team, year, 1, LeagueType.League, LeagueSetting.Default);
        Assert.Equal(1, capture.Last!.Projections[star.Id].Availability); // an old injury/non-starter flag cannot exclude him
        Assert.Equal(7.5f, capture.Last.Projections[star.Id].FantaAverage); // data actually persisted in PostgreSQL
        Assert.Contains(fallback.Warnings, x => x.Contains("non aggiornate"));

        // The provider confirms the same content: the existing injury becomes authoritative input again.
        await store.PublishChancesAsync(clock.GetUtcNow(), payload);
        await algorithm.CalculateAsync(team, year, 1, LeagueType.League, LeagueSetting.Default);
        Assert.Equal(0, capture.Last!.Projections[star.Id].Availability);
        Assert.False((await reader.ReadAsync(year, 1, true, true)).ChancesAreStale);
        Assert.Equal(2, await database.FootballPlayerStatistics.CountAsync());
    }

    private sealed class CaptureEngine : IOptimalFormationEngine
    {
        public OptimalFormationInput? Last { get; private set; }
        public OptimalFormationResult Calculate(OptimalFormationInput input)
        { Last = input; return new([], "", 0, 0, []); }
    }
}
