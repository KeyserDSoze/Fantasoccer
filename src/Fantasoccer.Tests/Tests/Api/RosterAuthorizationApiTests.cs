using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fantasoccer.Application.Api.Mcp;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Api;

[Collection("API integration")]
public sealed class RosterAuthorizationApiTests(ApiFactory factory)
{
    private const string Owner = "roster-owner@example.com";
    private const string CoOwner = "roster-coowner@example.com";
    private const string Admin = "roster-admin@example.com";
    private const string Basket = "basket";
    private const string League = "league";
    private const int Year = 2098;

    [Theory]
    [InlineData(Owner)]
    [InlineData(CoOwner)]
    public async Task Ownership_never_grants_direct_roster_insert_update_or_delete(string user)
    {
        var group = await SeedAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", user);
        var operations = factory.Services.GetRequiredService<McpEndpointCatalog>().Operations.Values.Where(x =>
            x.Api.RelativePath!.StartsWith("api/Team", StringComparison.OrdinalIgnoreCase) &&
            x.Api.RelativePath.Split('/').Last() is "Insert" or "Update" or "Delete").ToList();
        Assert.True(operations.Count >= 12, string.Join('\n', operations.Select(x => x.Api.RelativePath)));
        foreach (var operation in operations)
        {
            var path = operation.Api.RelativePath!;
            var key = path.Contains("/openingdaily/")
                ? KeySettings<OpeningTeamDayKey>.Instance.AsString(new() { Group = group, League = League, Year = Year, BasketId = Basket, Email = Owner, Day = 1 })
                : path.Contains("/opening/")
                    ? KeySettings<OpeningTeamKey>.Instance.AsString(new() { Group = group, League = League, Year = Year, BasketId = Basket, Email = Owner })
                    : path.Contains("/day/")
                        ? KeySettings<TeamDayKey>.Instance.AsString(new() { Group = group, Year = Year, BasketId = Basket, Email = Owner, Day = 1 })
                        : KeySettings<TeamKey>.Instance.AsString(new() { Group = group, Year = Year, BasketId = Basket, Email = Owner });
            using var request = new HttpRequestMessage(new HttpMethod(operation.Api.HttpMethod!), "/" + operation.Api.RelativePath + "?key=" + Uri.EscapeDataString(key));
            var forged = Team();
            forged.Players.Clear();
            forged.MoneyFromRank = 999999;
            request.Content = JsonContent.Create(forged);
            var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                operation.Api.RelativePath + ": " + response.StatusCode + " " + await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            if (path == "api/Team/Update")
            {
                using var lowerCase = new HttpRequestMessage(new HttpMethod(operation.Api.HttpMethod!), "/api/team/update?key=" + Uri.EscapeDataString(key))
                {
                    Content = JsonContent.Create(forged)
                };
                var lowerResponse = await client.SendAsync(lowerCase, TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Forbidden, lowerResponse.StatusCode);
            }
        }
        foreach (var operation in factory.Services.GetRequiredService<McpEndpointCatalog>().Operations.Values.Where(x =>
            x.Api.RelativePath!.StartsWith("api/Team/", StringComparison.OrdinalIgnoreCase) && x.Api.RelativePath.Contains("/Batch")))
        {
            var response = await client.PostAsJsonAsync("/" + operation.Api.RelativePath, new { }, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        var stored = await factory.Services.GetRequiredService<IRepository<Team, TeamKey>>().GetAsync(
            new TeamKey { Group = group, Year = Year, BasketId = Basket, Email = Owner }, TestContext.Current.CancellationToken);
        Assert.Equal(JsonSerializer.Serialize(Team()), JsonSerializer.Serialize(stored));
    }

    [Fact]
    public async Task Group_admin_can_still_change_roster_through_the_admin_repository_flow()
    {
        var group = await SeedAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Admin);
        var key = new TeamKey { Group = group, Year = Year, BasketId = Basket, Email = Owner };
        var changed = Team();
        changed.Players.RemoveAt(0);
        var response = await client.PostAsJsonAsync("/api/Team/Update?key=" + Uri.EscapeDataString(KeySettings<TeamKey>.Instance.AsString(key)), changed, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var stored = await factory.Services.GetRequiredService<IRepository<Team, TeamKey>>().GetAsync(key, TestContext.Current.CancellationToken);
        Assert.Equal(changed.Players.Count, stored!.Players.Count);
    }

    [Fact]
    public async Task Opening_formation_never_persists_submitted_sales_prices_money_or_removed_players()
    {
        var group = await SeedAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var submitted = Team();
        submitted.MoneyFromRank = 999999;
        submitted.OpeningCompetitionPrize = 999999;
        submitted.AdditionalOwners = ["attacker@example.com"];
        submitted.Players.RemoveAt(submitted.Players.Count - 1);
        submitted.Players[0].Price = 0;
        submitted.Players[0].Revenue = 999999;
        submitted.Players[0].Status = PlayerInTeamStatus.Sold;
        var response = await client.PostAsJsonAsync($"/OpeningCompetition/SaveTeam?groupId={group}&leagueId={League}&year={Year}&day=1", submitted, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await factory.Services.GetRequiredService<IRepository<Team, OpeningTeamDayKey>>().GetAsync(
            new OpeningTeamDayKey { Group = group, League = League, Year = Year, BasketId = Basket, Email = Owner, Day = 1 }, TestContext.Current.CancellationToken);
        Assert.Equal(JsonSerializer.Serialize(Team()), JsonSerializer.Serialize(stored));
    }

    [Fact]
    public async Task Opening_owner_can_still_change_formation_positions()
    {
        var group = await SeedAsync();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", Owner);
        var submitted = Team();
        var starter = submitted.Players.First(x => x.Position == FantaSoccerRole.Forward);
        var backup = submitted.Players.First(x => x.Position == FantaSoccerRole.FirstBackupForward);
        (starter.Position, backup.Position) = (backup.Position, starter.Position);
        var response = await client.PostAsJsonAsync($"/OpeningCompetition/SaveTeam?groupId={group}&leagueId={League}&year={Year}&day=1", submitted, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        Assert.Contains("\"i\":true", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var stored = await factory.Services.GetRequiredService<IRepository<Team, OpeningTeamDayKey>>().GetAsync(
            new OpeningTeamDayKey { Group = group, League = League, Year = Year, BasketId = Basket, Email = Owner, Day = 1 }, TestContext.Current.CancellationToken);
        Assert.Equal(JsonSerializer.Serialize(submitted.Players), JsonSerializer.Serialize(stored!.Players));
    }

    private async Task<string> SeedAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var id = "roster-security-" + Guid.NewGuid().ToString("N");
        var settings = LeagueSetting.Default;
        settings.OpeningCompetition = new OpeningCompetitionSettings { Enabled = true, SerieADays = 1, Prizes = [] };
        await factory.Services.GetRequiredService<IRepository<Group, string>>().InsertAsync(id, new Group
        {
            Id = id, Name = "Roster security", Users =
            [
                new() { Email = Owner, Role = IdentityRole.Participant },
                new() { Email = CoOwner, Role = IdentityRole.Participant },
                new() { Email = Admin, Role = IdentityRole.Admin }
            ],
            Leagues = [new() { Id = League, Name = "League", IsMain = true, Type = LeagueType.League, BasketsId = [Basket],
                Years = [new() { Year = Year, Settings = settings }] }],
            Baskets = [new() { Id = Basket, Name = "Basket", Years = [YearOfBasket(Year), YearOfBasket(Year - 1)] }]
        }, cancellationToken);
        foreach (var user in new[] { Owner, CoOwner, Admin })
            await factory.Services.GetRequiredService<IRepository<AppIdentity, string>>().InsertAsync(user,
                new AppIdentity { Email = user, Username = user, Groups = [new() { Id = id, Name = "Roster security", Role = user == Admin ? IdentityRole.Admin : IdentityRole.Participant }] }, cancellationToken);
        await factory.Services.GetRequiredService<IRepository<Team, TeamKey>>().InsertAsync(
            new TeamKey { Group = id, Year = Year, BasketId = Basket, Email = Owner }, Team(), cancellationToken);
        await factory.Services.GetRequiredService<IRepository<Team, TeamDayKey>>().InsertAsync(
            new TeamDayKey { Group = id, Year = Year, BasketId = Basket, Email = Owner, Day = 1 }, Team(), cancellationToken);
        await factory.Services.GetRequiredService<IRepository<Team, OpeningTeamKey>>().InsertAsync(
            new OpeningTeamKey { Group = id, League = League, Year = Year, BasketId = Basket, Email = Owner }, Team(), cancellationToken);
        await factory.Services.GetRequiredService<IRepository<Team, OpeningTeamDayKey>>().InsertAsync(
            new OpeningTeamDayKey { Group = id, League = League, Year = Year, BasketId = Basket, Email = Owner, Day = 1 }, Team(), cancellationToken);
        return id;
    }

    private static YearlyBasket YearOfBasket(int year) => new()
    {
        Year = year, Teams = [new() { Owner = Owner, Name = "Roster team", AdditionalOwners = [CoOwner] }]
    };

    private static Team Team()
    {
        var players = new List<Player>();
        Add(Role.GoalKeeper, FantaSoccerRole.GoalKeeper, 1);
        Add(Role.Defensor, FantaSoccerRole.Defensor, 3);
        Add(Role.Midfielder, FantaSoccerRole.Midfielder, 4);
        Add(Role.Forward, FantaSoccerRole.Forward, 3);
        Add(Role.Forward, FantaSoccerRole.FirstBackupForward, 1);
        return new Team { Name = "Roster team", Owner = Owner, AdditionalOwners = [CoOwner], Players = players, MoneyFromRank = 42, OpeningCompetitionPrize = 3 };
        void Add(Role role, FantaSoccerRole position, int count)
        {
            players.AddRange(Enumerable.Range(1, count).Select(i => new Player
            {
                Name = $"{role}-{position}-{(char)('A' + i)}", Role = role, Position = position, Price = 10,
                Status = PlayerInTeamStatus.Active, RealTeam = new RealTeam { Name = "Roma" }
            }));
        }
    }
}
