using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Formation_automation_creates_only_a_missing_future_day_with_current_finances_and_prior_positions_without_resetting_submissions()
    {
        using var manager = await factory.AccountAsync(globalAdmin: true);
        var account = (await manager.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = await CreateGroupAsync(manager);
        const int year = 29;
        await CreateLeagueAsync(manager, group.GroupId, year);
        var graph = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var result = await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(graph.Baskets[0].Id, year, "Automatic team", account.Id, []));
        result.EnsureSuccessStatusCode();
        var team = (await result.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        await using (var database = factory.Database.CreateContext())
        {
            var club = new FootballClub { Name = "Formation automation home", Abbreviation = "fah" }; var away = new FootballClub { Name = "Formation automation away", Abbreviation = "faa" };
            var player = new FootballPlayer { Name = "Automatic player " + Guid.NewGuid().ToString("N") };
            database.Clubs.AddRange(club, away); database.Players.Add(player);
            database.PlayerSeasons.Add(new() { Player = player, Club = club, SeasonIndex = year, Position = PlayerPosition.Forward });
            var seasonal = await database.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Season);
            seasonal.MoneyFromRank = 30; seasonal.MarketBalanceAdjustment = -5;
            seasonal.Players.Add(new() { Ordinal = 0, Player = player, Club = club, Price = 50, Position = 11, PlayerRole = 3 });
            database.RosterSnapshots.Add(new() { GroupId = group.GroupId, BasketId = graph.Baskets[0].Id, TeamId = team, SeasonIndex = year, TeamOriginYear = year,
                Kind = RosterKind.Matchday, Day = 3, Name = "Old daily name", OwnerUserId = account.Id, FormationChanges = 2,
                Players = [new() { Ordinal = 0, Player = player, Club = club, Price = 15, Position = 3, PlayerRole = 3 }], FormationHistory = [new() { Ordinal = 0, Author = "Historical author" }] });
            database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = 4, Matches = [new() { SeasonIndex = year, SerieADay = 4, Ordinal = 0, HomeClub = club, AwayClub = away, KickoffUtc = DateTimeOffset.UtcNow.AddDays(1) }] });
            await database.SaveChangesAsync();
        }
        async Task Prepare()
        { await using var scope = factory.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<FormationAutomationService>().PrepareNextAsync(year); }
        await Task.WhenAll(Prepare(), Prepare());
        await using (var database = factory.Database.CreateContext())
        {
            var daily = await database.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Matchday && x.Day == 4);
            Assert.Equal(50, Assert.Single(daily.Players).Price); Assert.Equal(3, daily.Players.Single().Position);
            Assert.Equal(30, daily.MoneyFromRank); Assert.Equal(-5, daily.MarketBalanceAdjustment);
            Assert.Equal(0, daily.FormationChanges); Assert.Empty(daily.FormationHistory);
            daily.Players.Single().Position = 11; daily.Version = 77; daily.FormationChanges = 1;
            await database.SaveChangesAsync();
        }
        await Prepare();
        await using var final = factory.Database.CreateContext();
        var preserved = await final.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Matchday && x.Day == 4);
        Assert.Equal(77, preserved.Version); Assert.Equal(11, preserved.Players.Single().Position); Assert.Equal(1, preserved.FormationChanges);
        Assert.Equal(2, (await final.RosterSnapshots.SingleAsync(x => x.TeamId == team && x.Kind == RosterKind.Matchday && x.Day == 3)).FormationChanges);
    }
}
