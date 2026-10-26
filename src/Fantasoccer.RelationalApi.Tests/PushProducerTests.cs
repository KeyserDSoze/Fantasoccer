using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Notifications;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task SQL_matchday_producer_commits_bonus_baseline_with_outbox_and_never_replays_unchanged_or_imported_reminders()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var group = await CreateGroupAsync(owner);
        var year = FantacalcioTime.ActualYear; var league = await CreateLeagueAsync(owner, group.GroupId, year);
        var graph = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var teamResponse = await owner.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(graph.Baskets[0].Id, year, "Push team", account.Id, []));
        teamResponse.EnsureSuccessStatusCode();
        var team = (await teamResponse.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        (await owner.PatchAsJsonAsync("/api/push-notifications", new PushPreferencesPatch(DeploymentReminder: true))).EnsureSuccessStatusCode();
        Guid player;
        await using (var database = factory.Database.CreateContext())
        {
            (await database.Leagues.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == league)).IsMain = true;
            var club = new FootballClub { Name = "Push producer club", Abbreviation = "ppc" };
            var other = new FootballClub { Name = "Push producer opponent", Abbreviation = "ppo" };
            var person = new FootballPlayer { Name = "Push producer " + Guid.NewGuid().ToString("N") };
            database.Clubs.AddRange(club, other); database.Players.Add(person);
            database.PlayerSeasons.Add(new() { Player = person, Club = club, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(person.Name), Position = PlayerPosition.Forward });
            database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = 33, Matches =
                [new() { SeasonIndex = year, SerieADay = 33, HomeClub = club, AwayClub = other, KickoffUtc = DateTimeOffset.UtcNow.AddMinutes(-30) }] });
            database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = 34, Matches =
                [new() { SeasonIndex = year, SerieADay = 34, HomeClub = club, AwayClub = other, KickoffUtc = DateTimeOffset.UtcNow.AddHours(3) }] });
            database.FootballVotes.Add(new() { SeasonIndex = year, Day = 33, Source = FootballVoteSource.Live, Player = person, Club = club, PlayerRole = 3, VoteRole = 3, Goals = 1, Value = 7.5f });
            database.RosterSnapshots.Add(new() { GroupId = group.GroupId, BasketId = graph.Baskets[0].Id, SeasonIndex = year, TeamOriginYear = year, TeamId = team,
                Kind = RosterKind.Matchday, Day = 33, OwnerUserId = account.Id, Name = "Push team", Players = [new() { Ordinal = 0, Player = person, Club = club, PlayerRole = 3, Status = 0, Position = 3 }] });
            database.PushDayCheckpoints.Add(new() { SeasonIndex = year, Day = 34, DeploymentReminder = true });
            await database.SaveChangesAsync(); player = person.Id;
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<MatchdayPushProducer>().RunAsync(year);
            await scope.ServiceProvider.GetRequiredService<MatchdayPushProducer>().RunAsync(year);
        }
        await using (var database = factory.Database.CreateContext())
        {
            Assert.Equal(1, await database.PushDeliveries.CountAsync(x => x.UserId == account.Id));
            var state = await database.PushDayCheckpoints.SingleAsync(x => x.SeasonIndex == year && x.Day == 33);
            Assert.Equal(1, state.MatchEventSequence); Assert.Equal(1, state.Votes.Single(x => x.PlayerId == player).Goals);
            var vote = await database.FootballVotes.SingleAsync(x => x.SeasonIndex == year && x.Day == 33 && x.PlayerId == player && x.Source == FootballVoteSource.Live);
            vote.Goals = 0; await database.SaveChangesAsync();
        }
        await using (var scope = factory.Services.CreateAsyncScope()) await scope.ServiceProvider.GetRequiredService<MatchdayPushProducer>().RunAsync(year);
        await using var final = factory.Database.CreateContext();
        Assert.Equal(2, await final.PushDeliveries.CountAsync(x => x.UserId == account.Id));
        Assert.Contains(await final.PushDeliveries.Where(x => x.UserId == account.Id).ToArrayAsync(), x => x.Body.Contains("annullato"));
        Assert.False(await final.PushDeliveries.AnyAsync(x => x.UserId == account.Id && x.Kind == 2));
        Assert.Equal(2, (await final.PushDayCheckpoints.SingleAsync(x => x.SeasonIndex == year && x.Day == 33)).MatchEventSequence);
    }
}
