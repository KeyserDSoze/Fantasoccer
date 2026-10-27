using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Editorial;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Editorial_HTTP_keeps_legacy_reads_scoped_AI_disabled_by_default_and_generation_CAS_protects_saved_articles()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true); using var outsider = await factory.AccountAsync();
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = await CreateGroupAsync(owner); var year = FantacalcioTime.ActualYear; var league = await CreateLeagueAsync(owner, group.GroupId, year);
        var path = $"/api/groups/{group.GroupId}/news/{year}/8";
        await using (var database = factory.Database.CreateContext())
        {
            database.NewsEditions.Add(new() { GroupId = group.GroupId, SeasonIndex = year, Day = 8, Articles =
                [new() { Ordinal = 0, LeagueId = league, LeagueName = "Original", Title = "Original title", Content = "<p>Original HTML</p>", PublishedAtUtc = DateTimeOffset.UtcNow }] });
            database.FantasyCalendars.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Stages =
                [new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Stage = "@", Days =
                    [new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Stage = "@", SerieADay = 8, FantasyTurn = 1, Games =
                         [new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, Id = Guid.NewGuid(), Stage = "@", HomeName = "Home", AwayName = "Away", Result = new() { Home = new() { Value = 70 }, Away = new() { Value = 65 }, HomeGoals = 1, AwayGoals = 0 } }] }] }] });
            // A newer current rank must not be used as if it described day 8.
            database.FantasyStandingSnapshots.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = year, SnapshotDay = 0, SerieADay = 10 });
            await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(path)).StatusCode);
        Assert.Equal("legacy-html", (await owner.GetFromJsonAsync<NewsEditionDto>(path))!.Articles[0].Format);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await owner.PostAsJsonAsync(path + $"/{league}/generate", new GenerateNewsRequest(1))).StatusCode);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        var generator = new StubEditorial(async (_, facts) =>
        {
            Interlocked.Increment(ref calls); Assert.DoesNotContain(account.Email, facts);
            using var context = System.Text.Json.JsonDocument.Parse(facts);
            Assert.Equal(System.Text.Json.JsonValueKind.Null, context.RootElement.GetProperty("standings").ValueKind);
            entered.TrySetResult(); await release.Task; return "Titolo sintetico\nArticolo basato sui risultati salvati.";
        });
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IEditorialTextGenerator>(generator)));
        app.Services.GetRequiredService<IOptions<EditorialAiOptions>>().Value.Enabled = true;
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Workspace-User", account.Email);
        var generating = client.PostAsJsonAsync(path + $"/{league}/generate", new GenerateNewsRequest(1));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
        try
        {
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + $"/{league}/generate", new GenerateNewsRequest(1)).WaitAsync(TimeSpan.FromSeconds(10))).StatusCode);
            Assert.Equal(1, calls);
        }
        finally { release.TrySetResult(); }
        var response = await generating; response.EnsureSuccessStatusCode();
        var generated = (await response.Content.ReadFromJsonAsync<NewsEditionDto>())!;
        Assert.Equal("Titolo sintetico", generated.Articles[0].Title); Assert.Equal("plain-text", generated.Articles[0].Format);
        generator.Callback = async (_, _) =>
        {
            await using var database = factory.Database.CreateContext();
            (await database.FantasyCalendars.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == league)).Version++;
            await database.SaveChangesAsync(); return "Stale title\nMust not replace the saved article.";
        };
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + $"/{league}/generate", new GenerateNewsRequest(generated.Version))).StatusCode);
        Assert.Equal("Titolo sintetico", (await client.GetFromJsonAsync<NewsEditionDto>(path))!.Articles[0].Title);
    }

    [PostgreSqlFact]
    public async Task Team_advice_is_owner_only_and_AI_generation_never_writes_formation_or_financial_values()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true); using var reader = await factory.AccountAsync(); using var coOwner = await factory.AccountAsync();
        var first = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!; var second = (await reader.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var partner = (await coOwner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = await CreateGroupAsync(owner); var year = FantacalcioTime.ActualYear; await CreateLeagueAsync(owner, group.GroupId, year);
        (await owner.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(second.Email, "Reader", 1))).EnsureSuccessStatusCode();
        (await owner.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(partner.Email, "Co-owner", 2))).EnsureSuccessStatusCode();
        var graph = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var created = await owner.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(graph.Baskets[0].Id, year, "Advice team", first.Id, [partner.Id]));
        created.EnsureSuccessStatusCode(); var team = (await created.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        await using (var database = factory.Database.CreateContext())
        {
            var club = new FootballClub { Name = "Advice club", Abbreviation = "adv" }; database.Clubs.Add(club);
            var roster = await database.RosterSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.TeamId == team && x.Kind == RosterKind.Season);
            roster.MoneyFromRank = 41;
            var roles = new[] { 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3 };
            for (var index = 0; index < roles.Length; index++)
            {
                var player = new FootballPlayer { Name = "Advice player " + Guid.NewGuid().ToString("N") }; database.Players.Add(player);
                database.PlayerSeasons.Add(new() { Player = player, Club = club, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Position = (PlayerPosition)roles[index] });
                roster.Players.Add(new() { Ordinal = index, Player = player, Club = club, PlayerRole = roles[index], Position = roles[index], Price = 10 });
            }
            database.TeamAdvice.Add(new() { GroupId = group.GroupId, BasketId = graph.Baskets[0].Id, TeamId = team, OwnerUserId = first.Id, SeasonIndex = year, Day = 9, Message = "Original private message" });
            await database.SaveChangesAsync();
        }
        var path = $"/api/groups/{group.GroupId}/teams/{team}/advice/{year}/9";
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await coOwner.GetAsync(path)).StatusCode);
        var generator = new StubEditorial((_, _) => Task.FromResult("Testo esplicativo: nessuna scrittura della formazione."));
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddSingleton<IEditorialTextGenerator>(generator)));
        app.Services.GetRequiredService<IOptions<EditorialAiOptions>>().Value.Enabled = true;
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Workspace-User", first.Email);
        var response = await client.PostAsJsonAsync(path + "/generate", new GenerateAdviceRequest(1, 1)); response.EnsureSuccessStatusCode();
        Assert.True((await response.Content.ReadFromJsonAsync<TeamAdviceDto>())!.IsGenerated);
        Assert.Equal(HttpStatusCode.NoContent, (await coOwner.GetAsync(path)).StatusCode);
        await using var final = factory.Database.CreateContext();
        var saved = await final.RosterSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.TeamId == team && x.Kind == RosterKind.Season);
        Assert.Equal(1, saved.Version); Assert.Equal(41, saved.MoneyFromRank); Assert.All(saved.Players, x => Assert.Equal(10, x.Price));
    }
}
internal sealed class StubEditorial(Func<string, string, Task<string>> callback) : IEditorialTextGenerator
{
    public Func<string, string, Task<string>> Callback { get; set; } = callback;
    public Task<string> GenerateAsync(string instruction, string facts, CancellationToken cancellationToken) => Callback(instruction, facts);
}
