using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

// The official page marks everybody as "in": who started and who came on is taken from the live feed and kept on the vote rows.
public sealed class FootballInOutTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    private static CachedVote Flags(bool isIn, bool isOut) => new(1, 6, false, 0, 0, 0, 0, 0, 0, 0, 0, false, true, isOut, isIn, false);

    [PostgreSqlFact]
    public async Task The_official_import_keeps_who_came_on_and_who_went_off_from_the_live_feed()
    {
        using var account = await factory.AccountAsync(globalAdmin: true);
        var email = account.DefaultRequestHeaders.GetValues("X-Workspace-User").Single();
        var sources = new FootballIngestionApiTests.IngestionSources();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<ISerieAInformation>(sources); services.AddSingleton<IAllPlayers>(sources);
            services.AddSingleton<IOfficialVote>(sources); services.AddSingleton<ILiveVote>(sources);
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Workspace-User", email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [email];
        var year = FantacalcioTime.ActualYear;
        foreach (var operation in new[] { "calendar", "catalogue" }) (await client.PostAsJsonAsync("/api/football/refresh", new RefreshFootballRequest(operation, year))).EnsureSuccessStatusCode();
        string Key(string name) => KeyUtils.GetPlayerKey(name);
        await app.Services.GetRequiredService<IFootballLiveDataStore>().PublishLiveVotesAsync(DateTimeOffset.UtcNow, new(year, 1,
        [
            new CachedLiveVote(Key("Ingestion forward"), "Ingestion forward", "fio", "Fiorentina", 3, true, true, Flags(isIn: true, isOut: false)),
            new CachedLiveVote(Key("Ingestion defender"), "Ingestion defender", "nap", "Napoli", 1, true, true, Flags(isIn: false, isOut: true)),
        ]), null, default);
        (await client.PostAsJsonAsync("/api/football/refresh", new RefreshFootballRequest("official", year, 1))).EnsureSuccessStatusCode();
        await using var database = factory.Database.CreateContext();
        var rows = await database.FootballVotes.Include(x => x.Player).Where(x => x.SeasonIndex == year && x.Day == 1 && x.Source == FootballVoteSource.Official).ToArrayAsync();
        Assert.True(rows.Single(x => x.Player.Name == "Ingestion forward").IsIn);
        Assert.True(rows.Single(x => x.Player.Name == "Ingestion defender").IsOut);
        Assert.False(rows.Single(x => x.Player.Name == "Ingestion defender").IsIn);
    }

    [PostgreSqlFact]
    public async Task Votes_imported_with_everybody_in_are_corrected_from_the_live_feed_while_it_still_has_the_club_and_real_flags_are_never_overwritten()
    {
        const int year = 38; const int day = 30;
        var club = new FootballClub { Name = "Sync club", Abbreviation = "syc" };
        var players = Enumerable.Range(0, 14).Select(i => new FootballPlayer { Name = $"Sync player {i:00} {Guid.NewGuid():N}" }).ToArray();
        await using (var database = factory.Database.CreateContext())
        {
            if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == year)) database.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 });
            database.Clubs.Add(club); database.Players.AddRange(players);
            foreach (var player in players)
            {
                database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Club = club, Position = PlayerPosition.Defender, IsActive = true, IsVisible = true });
                database.FootballVotes.Add(new() { Player = player, Club = club, SeasonIndex = year, Day = day, Source = FootballVoteSource.Official, PlayerRole = 1, VoteRole = 1,
                    Value = 6, HasVote = true, IsIn = true, IsFinal = true, IsActive = true, IsVisible = true });
            }
            await database.SaveChangesAsync();
        }
        string Key(int i) => KeyUtils.GetPlayerKey(players[i].Name);
        // Eleven started (0..10), three came on (11..13), one of the starters went off (2).
        var live = Enumerable.Range(0, 14).Select(i => new CachedLiveVote(Key(i), players[i].Name, club.Id, club.Name, 1, true, true, Flags(isIn: i >= 11, isOut: i == 2))).ToArray();
        await factory.Services.GetRequiredService<IFootballLiveDataStore>().PublishLiveVotesAsync(DateTimeOffset.UtcNow, new(year, day, live), null, default);
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<FootballIngestionApplicationService>().SyncInOutFromLiveAsync(year, day, default));
        await using (var database = factory.Database.CreateContext())
        {
            var rows = await database.FootballVotes.Include(x => x.Player).Where(x => x.SeasonIndex == year && x.Day == day).ToArrayAsync();
            Assert.Equal(3, rows.Count(x => x.IsIn)); Assert.Equal(1, rows.Count(x => x.IsOut));
            Assert.All(rows.Where(x => x.Player.Name == players[11].Name || x.Player.Name == players[13].Name), x => Assert.True(x.IsIn));
        }
        // Already real (not everybody is in): a later correction, with a different live feed, changes nothing.
        await factory.Services.GetRequiredService<IFootballLiveDataStore>().PublishLiveVotesAsync(DateTimeOffset.UtcNow.AddMinutes(1), new(year, day,
            live.Select(x => x with { Vote = Flags(isIn: true, isOut: false) }).ToArray()), null, default);
        await using (var scope = factory.Services.CreateAsyncScope())
            Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<FootballIngestionApplicationService>().SyncInOutFromLiveAsync(year, day, default));
        await using (var database = factory.Database.CreateContext())
            Assert.Equal(3, await database.FootballVotes.CountAsync(x => x.SeasonIndex == year && x.Day == day && x.IsIn));
    }
}
