using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed class FootballIngestionApiTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    [PostgreSqlFact]
    public async Task Shared_ingestion_updates_only_SQL_rejects_empty_and_incomplete_sources_and_calculates_statistics_without_erasing_descriptions()
    {
        using var account = await factory.AccountAsync(globalAdmin: true);
        var email = account.DefaultRequestHeaders.GetValues("X-Workspace-User").Single();
        var sources = new IngestionSources();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<ISerieAInformation>(sources); services.AddSingleton<IAllPlayers>(sources);
            services.AddSingleton<IOfficialVote>(sources); services.AddSingleton<ILiveVote>(sources);
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Workspace-User", email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [email];
        var year = FantacalcioTime.ActualYear;
        const string path = "/api/football/refresh";
        var calendar = await client.PostAsJsonAsync(path, new RefreshFootballRequest("calendar", year));
        Assert.True(calendar.IsSuccessStatusCode, await calendar.Content.ReadAsStringAsync());
        (await client.PostAsJsonAsync(path, new RefreshFootballRequest("catalogue", year))).EnsureSuccessStatusCode();
        Guid[] identities;
        await using (var database = factory.Database.CreateContext())
        {
            identities = await database.Players.OrderBy(x => x.Name).Select(x => x.Id).ToArrayAsync();
            Assert.Equal(2, identities.Length); Assert.Equal(1, await database.FootballMatches.CountAsync());
        }
        (await client.PostAsJsonAsync(path, new RefreshFootballRequest("catalogue", year))).EnsureSuccessStatusCode();
        var catalogue = (await client.GetFromJsonAsync<PlayersDto<FootballPlayerDto>>($"/api/football/{year}/players"))!;
        Assert.All(catalogue.Players, player =>
        {
            Assert.Equal(year, player.SeasonIndex); Assert.Equal(KeyUtils.GetSeasonPlayerKey(player.Name, year), player.PlayerKey);
        });
        sources.InvalidCatalogueRole = true;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new RefreshFootballRequest("catalogue", year))).StatusCode);
        sources.InvalidCatalogueRole = false; sources.DuplicateCatalogueKey = true;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new RefreshFootballRequest("catalogue", year))).StatusCode);
        sources.DuplicateCatalogueKey = false;
        sources.EmptyCatalogue = true;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new RefreshFootballRequest("catalogue", year))).StatusCode);
        sources.EmptyCatalogue = false; sources.IncompleteOfficial = true;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new RefreshFootballRequest("official", year, 1))).StatusCode);
        await using (var database = factory.Database.CreateContext()) Assert.Equal(0, await database.FootballVotes.CountAsync());
        sources.IncompleteOfficial = false;
        (await client.PostAsJsonAsync(path, new RefreshFootballRequest("official", year, 1))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(path, new RefreshFootballRequest("statistics", year, 2))).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
        {
            Assert.Equal(identities, await database.Players.OrderBy(x => x.Name).Select(x => x.Id).ToArrayAsync());
            var statistics = await database.FootballPlayerStatistics.Include(x => x.Player).SingleAsync(x => x.Player.Name == "Ingestion forward");
            Assert.Equal(1, statistics.WithVote); Assert.Equal(1, statistics.NoPlayed); Assert.Equal(1, statistics.Goals);
            Assert.Equal(2, statistics.Games.Count); statistics.HasDescription = true; statistics.DescriptionTop = "Retain editorial description";
            await database.SaveChangesAsync();
        }
        (await client.PostAsJsonAsync(path, new RefreshFootballRequest("statistics", year, 2))).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext()) Assert.Equal("Retain editorial description", (await database.FootballPlayerStatistics.Include(x => x.Player).SingleAsync(x => x.Player.Name == "Ingestion forward")).DescriptionTop);
        using var ordinary = await factory.AccountAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await ordinary.PostAsJsonAsync(path, new RefreshFootballRequest("catalogue", year))).StatusCode);
    }
    private sealed class IngestionSources : ISerieAInformation, IAllPlayers, ILiveVote, IOfficialVote
    {
        public bool EmptyCatalogue { get; set; }
        public bool IncompleteOfficial { get; set; }
        public bool InvalidCatalogueRole { get; set; }
        public bool DuplicateCatalogueKey { get; set; }
        private static RealPlayer[] Players => [new() { Name = "Ingestion forward", RealTeam = new() { Name = "Ingestion home", Abbreviation = "inh" }, Role = Role.Forward, IsActive = true },
            new() { Name = "Ingestion defender", RealTeam = new() { Name = "Ingestion away", Abbreviation = "ina" }, Role = Role.Defensor, IsActive = true }];
        public Task<RealCalendar> GetCalendarAsync(int min = 1, int max = 38) => Task.FromResult(new RealCalendar { Year = FantacalcioTime.ActualYear, Days =
            [new() { Year = FantacalcioTime.ActualYear, SerieADay = 1, Games = [new() { Home = Players[0].RealTeam, Away = Players[1].RealTeam, Date = FantacalcioTime.ItalyNow.Date.AddDays(-1).AddHours(12), HomeGoal = 1, AwayGoal = 0 }] }] });
        public Task<List<RealPlayer>> GetAsync()
        {
            var players = EmptyCatalogue ? new List<RealPlayer>() : Players.ToList();
            if (InvalidCatalogueRole) players[0].Role = (Role)99;
            if (DuplicateCatalogueKey) players.Add(new() { Name = "INGESTION-FORWARD", RealTeam = Players[0].RealTeam, Role = Role.Forward });
            return Task.FromResult(players);
        }
        public Task<List<VotedRealPlayer>> GetAsync(int year, int day) => Task.FromResult(Players.Take(IncompleteOfficial ? 1 : 2).Select((player, index) => new VotedRealPlayer
            { Name = player.Name, RealTeam = player.RealTeam, Role = player.Role, IsActive = true, Vote = new() { Role = player.Role, HasVote = true, IsFinal = true, Value = 7, Goal = index == 0 ? 1 : 0 } }).ToList());
    }
}
