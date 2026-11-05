using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Operations;
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

public sealed class FootballOperationalCacheApiTests(WorkspaceApiFactory factory) : IClassFixture<WorkspaceApiFactory>
{
    [PostgreSqlFact]
    public async Task Live_votes_and_probable_lineups_are_one_global_cache_snapshot_and_invalid_sources_never_replace_it()
    {
        using var account = await factory.AccountAsync(globalAdmin: true);
        var email = account.DefaultRequestHeaders.GetValues("X-Workspace-User").Single();
        var sources = new Sources();
        var parsers = new Parsers();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<ISerieAInformation>(sources); services.AddSingleton<IAllPlayers>(sources); services.AddSingleton<ILiveVote>(sources);
            services.AddSingleton<IFactory<IFormationParser>>(parsers);
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Workspace-User", email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [email];
        var year = FantacalcioTime.ActualYear;
        const string path = "/api/football/refresh";
        (await client.PostAsJsonAsync(path, new RefreshFootballRequest("calendar", year))).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync(path, new RefreshFootballRequest("catalogue", year))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/football/{year}/days/1/votes/live")).StatusCode);

        (await client.PostAsJsonAsync(path, new RefreshFootballRequest("live", year, 1))).EnsureSuccessStatusCode();
        var live = (await client.GetFromJsonAsync<PlayersDto<VotedPlayerDto>>($"/api/football/{year}/days/1/votes/live"))!;
        Assert.Equal(2, live.Players.Length);
        Assert.Equal(1, live.Players.Single(x => x.Name == "Cache forward").Vote.Goals);
        Assert.Equal("fio", live.Players.Single(x => x.Name == "Cache forward").RealTeam!.ClubId);
        await using (var database = factory.Database.CreateContext())
        {
            // PostgreSQL is not the operational store of provisional votes.
            Assert.False(await database.FootballVotes.AnyAsync(x => x.Source == FootballVoteSource.Live));
            Assert.False(await database.FootballDatasets.AnyAsync(x => x.Kind == "live"));
        }
        sources.EmptyLive = true;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new RefreshFootballRequest("live", year, 1))).StatusCode);
        sources.EmptyLive = false; sources.DuplicateLive = true;
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new RefreshFootballRequest("live", year, 1))).StatusCode);
        sources.DuplicateLive = false;
        Assert.Equal(2, (await client.GetFromJsonAsync<PlayersDto<VotedPlayerDto>>($"/api/football/{year}/days/1/votes/live"))!.Players.Length);

        parsers.Set("Fantagazzetta", [Chance("Cache forward", "Fiorentina", new() { Fantagazzetta = true, Status = ChanceType.Normal })]);
        parsers.Set("Gazzetta", [Chance("Cache defender", "Napoli", new() { Gazzetta = true, Status = ChanceType.Warned, Description = "Diffidato" })]);
        parsers.Set("Injury", [Chance("Cache forward", "Fiorentina", new() { Status = ChanceType.Injury, Description = "Lesione" }),
            Chance("Cache keeper", "Napoli", new() { Status = ChanceType.Disqualified, Description = "Squalificato" })]);
        (await client.PostAsJsonAsync(path, new RefreshFootballRequest("chances", year, 1))).EnsureSuccessStatusCode();
        var chances = (await client.GetFromJsonAsync<PlayersDto<ChancedPlayerDto>>($"/api/football/{year}/days/1/chances"))!;
        var forward = chances.Players.Single(x => x.Name == "Cache forward");
        // Flags OR-ed across sources; status/description from the last reporting source.
        Assert.True(forward.Chance.Fantagazzetta); Assert.Equal((int)ChanceType.Injury, forward.Chance.Status); Assert.Equal("Lesione", forward.Chance.Description);
        Assert.Equal((int)ChanceType.Warned, chances.Players.Single(x => x.Name == "Cache defender").Chance.Status);
        Assert.Equal((int)ChanceType.Disqualified, chances.Players.Single(x => x.Name == "Cache keeper").Chance.Status);
        await using (var database = factory.Database.CreateContext()) Assert.False(await database.FootballChances.AnyAsync());
        parsers.Set("Injury", []);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, new RefreshFootballRequest("chances", year, 1))).StatusCode);
        Assert.Equal("Lesione", (await client.GetFromJsonAsync<PlayersDto<ChancedPlayerDto>>($"/api/football/{year}/days/1/chances"))!
            .Players.Single(x => x.Name == "Cache forward").Chance.Description);

        // The scoring/optimal-formation input reads the very same snapshots.
        await using var scope = app.Services.CreateAsyncScope();
        var input = await scope.ServiceProvider.GetRequiredService<IFootballMatchdayReader>().ReadAsync(year, 1, true, true);
        Assert.Equal(3, input.Chances!.Players.Count);
        Assert.Equal(ChanceType.Injury, input.Chances.Players.Single(x => x.Name == "Cache forward").Chance.Status);
        Assert.Equal(1, input.Live!.Players.Single(x => x.Name == "Cache forward").Vote.Goal);
        Assert.Null(input.Official);
    }

    [PostgreSqlFact]
    public async Task Chances_job_names_the_empty_source_and_waits_while_lineups_are_not_published()
    {
        using var account = await factory.AccountAsync(globalAdmin: true);
        var email = account.DefaultRequestHeaders.GetValues("X-Workspace-User").Single();
        var sources = new Sources(); var parsers = new Parsers();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<ISerieAInformation>(sources); services.AddSingleton<IAllPlayers>(sources); services.AddSingleton<ILiveVote>(sources);
            services.AddSingleton<IFactory<IFormationParser>>(parsers);
        }));
        using var client = app.CreateClient();
        client.DefaultRequestHeaders.Add("X-Workspace-User", email); client.DefaultRequestHeaders.Add("X-Workspace-Admin", "true");
        app.Services.GetRequiredService<IOptions<GlobalAdministratorOptions>>().Value.Emails = [email];
        var year = FantacalcioTime.ActualYear;
        foreach (var operation in new[] { "calendar", "catalogue" }) (await client.PostAsJsonAsync("/api/football/refresh", new RefreshFootballRequest(operation, year))).EnsureSuccessStatusCode();
        async Task KickoffIn(TimeSpan span)
        {
            await using var database = factory.Database.CreateContext();
            var match = await database.FootballMatches.SingleAsync(x => x.SeasonIndex == year && x.SerieADay == 1);
            match.KickoffUtc = DateTimeOffset.UtcNow + span; match.IsFinished = null; await database.SaveChangesAsync();
        }
        async Task<(SqlJobResult Run, OperationalLogDto Log)> Run()
        {
            var run = (await (await client.PostAsJsonAsync("/api/operations/jobs/run", new RunSqlJobRequest("chances", year))).Content.ReadFromJsonAsync<SqlJobResult>())!;
            return (run, (await client.GetFromJsonAsync<OperationalLogDto>($"/api/operations/logs/{run.RunId}"))!);
        }
        parsers.Set("Fantagazzetta", []); parsers.Set("Gazzetta", [Chance("Cache defender", "Napoli", new() { Gazzetta = true })]);
        parsers.Set("Injury", [Chance("Cache keeper", "Napoli", new() { Status = ChanceType.Disqualified, Description = "Squalificato" })]);

        // Lineups of a faraway matchday are not published yet: an expected wait, not retried every minute.
        await KickoffIn(TimeSpan.FromDays(5));
        var waiting = await Run();
        Assert.Equal("Partial", waiting.Run.Status); Assert.Equal("chances_lineups_pending", waiting.Run.Code);
        Assert.Contains("senza giocatori: Fantagazzetta", waiting.Log.Detail); Assert.Contains("Gazzetta: 1", waiting.Log.Detail);
        await using (var database = factory.Database.CreateContext())
            Assert.True((await database.SqlJobStates.SingleAsync(x => x.Name == "chances" && x.SeasonIndex == year)).NextRunAtUtc > DateTimeOffset.UtcNow.AddMinutes(5));

        // The matchday is close: the same gap is a failure and the log says which source is empty.
        await KickoffIn(TimeSpan.FromHours(10));
        var failed = await Run();
        Assert.Equal("Failed", failed.Run.Status); Assert.Equal("chances_lineups_unavailable", failed.Run.Code);
        Assert.Contains("Fantagazzetta", failed.Log.Detail); Assert.Contains("Giornata 1", failed.Log.Detail);

        // The injuries source empty is always a failure, even far from the matchday.
        await KickoffIn(TimeSpan.FromDays(5)); parsers.Set("Fantagazzetta", [Chance("Cache forward", "Fiorentina", new() { Fantagazzetta = true })]); parsers.Set("Injury", []);
        var injuries = await Run();
        Assert.Equal("Failed", injuries.Run.Status); Assert.Equal("football_provider_incomplete", injuries.Run.Code); Assert.Contains("senza giocatori: Injury", injuries.Log.Detail);

        // All three sources answer: the log says what was read and published.
        parsers.Set("Injury", [Chance("Cache keeper", "Napoli", new() { Status = ChanceType.Disqualified, Description = "Squalificato" })]);
        var ok = await Run();
        Assert.Equal("Succeeded", ok.Run.Status);
        Assert.Contains("Probabilità della giornata 1", ok.Log.Detail); Assert.Contains("Fantagazzetta: 1", ok.Log.Detail); Assert.Contains("con infortunio, squalifica o diffida", ok.Log.Detail);
    }

    private static ChancedRealPlayer Chance(string name, string club, Chance chance) => new() { Name = name, RealTeam = new() { Name = club }, Role = Role.Forward, Chance = chance };

    private sealed class Parsers : IFactory<IFormationParser>
    {
        private readonly Dictionary<string, List<ChancedRealPlayer>> _values = [];
        public void Set(string name, List<ChancedRealPlayer> players) => _values[name] = players;
        public IFormationParser? Create(AnyOf<string?, Enum>? name = null) => new Parser(_values.GetValueOrDefault(name?.ToString() ?? "") ?? []);
        public IFormationParser? CreateWithoutDecoration(AnyOf<string?, Enum>? name = null) => Create(name);
        public IEnumerable<IFormationParser> CreateAll(AnyOf<string?, Enum>? name = null) => _values.Values.Select(x => new Parser(x));
        public IEnumerable<IFormationParser> CreateAllWithoutDecoration(AnyOf<string?, Enum>? name = null) => CreateAll(name);
        public bool Exists(AnyOf<string?, Enum>? name = null) => true;
        private sealed class Parser(List<ChancedRealPlayer> players) : IFormationParser { public Task<List<ChancedRealPlayer>> GetPlayersAsync() => Task.FromResult(players.ToList()); }
    }

    private sealed class Sources : ISerieAInformation, IAllPlayers, ILiveVote
    {
        public bool EmptyLive { get; set; }
        public bool DuplicateLive { get; set; }
        private static RealPlayer[] Players => [new() { Name = "Cache forward", RealTeam = new() { Name = "Fiorentina" }, Role = Role.Forward, IsActive = true },
            new() { Name = "Cache defender", RealTeam = new() { Name = "Napoli" }, Role = Role.Defensor, IsActive = true },
            new() { Name = "Cache keeper", RealTeam = new() { Name = "Napoli" }, Role = Role.GoalKeeper, IsActive = true }];
        public Task<RealCalendar> GetCalendarAsync(int min = 1, int max = 38) => Task.FromResult(new RealCalendar { Year = FantacalcioTime.ActualYear, Days =
            [new() { Year = FantacalcioTime.ActualYear, SerieADay = 1, Games = [new() { Home = Players[0].RealTeam, Away = Players[1].RealTeam, Date = FantacalcioTime.ItalyNow.Date.AddDays(-1).AddHours(12), HomeGoal = 1, AwayGoal = 0 }] }] });
        public Task<RealCalendar> GetCalendarForSeasonAsync(int year, int min, int max) => GetCalendarAsync(min, max);
        public Task<List<RealPlayer>> GetAsync() => Task.FromResult(Players.ToList());
        public Task<List<VotedRealPlayer>> GetAsync(int year, int day)
        {
            if (EmptyLive) return Task.FromResult(new List<VotedRealPlayer>());
            var votes = Players.Take(2).Select((player, index) => new VotedRealPlayer { Name = player.Name, RealTeam = player.RealTeam, Role = player.Role, IsActive = true,
                Vote = new() { Role = player.Role, HasVote = true, Value = 6.5f, Goal = index == 0 ? 1 : 0 } }).ToList();
            if (DuplicateLive) votes.Add(votes[0]);
            return Task.FromResult(votes);
        }
    }
}
