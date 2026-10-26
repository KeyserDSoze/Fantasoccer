using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Opening_preparation_uses_previous_teams_current_competition_season_and_preserves_versions_and_authorization()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        using var outsider = await factory.AccountAsync();
        var group = await CreateGroupAsync(owner);
        var league = await CreateLeagueAsync(owner, group.GroupId, 21);
        var account = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var graph = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var basket = graph.Baskets[0].Id;
        var teamResponse = await owner.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, 21, "Previous team", account.Id, []));
        teamResponse.EnsureSuccessStatusCode();
        var teamId = (await teamResponse.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId;
        await using (var database = factory.Database.CreateContext())
        {
            database.Seasons.Add(new() { SeasonIndex = 22, StartYear = 2033 });
            var competition = await database.Leagues.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == league);
            competition.IsMain = true;
            database.LeagueSeasons.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = 22, CompetitionType = 1 });
            var rules = LeagueRulesMapping.Default(group.GroupId, league, 22); rules.OpeningCompetitionEnabled = true;
            database.LeagueRules.Add(rules);
            var club = new FootballClub { Name = "Opening club", Abbreviation = "opc" };
            var away = new FootballClub { Name = "Opening away", Abbreviation = "opa" };
            database.Clubs.AddRange(club, away);
            var source = await database.RosterSnapshots.SingleAsync(x => x.TeamId == teamId && x.Kind == RosterKind.Season);
            var roles = new[] { 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3 };
            for (var i = 0; i < roles.Length; i++)
            {
                var person = new FootballPlayer { Name = "Opening player " + Guid.NewGuid().ToString("N") };
                database.Players.Add(person);
                database.PlayerSeasons.Add(new() { Player = person, Club = club, SeasonIndex = 21, NormalizedName = KeyUtils.GetPlayerKey(person.Name), Position = (PlayerPosition)roles[i] });
                source.Players.Add(new() { Ordinal = i, Player = person, Club = club, PlayerRole = roles[i], Status = 0, Position = roles[i], Price = 10 });
                if (i == 0) person.Name = "Updated opening display label"; // The latched natural key is still authoritative.
            }
            database.FootballMatchdays.Add(new() { SeasonIndex = 22, SerieADay = 1, Matches =
                [new() { SeasonIndex = 22, SerieADay = 1, Ordinal = 0, HomeClub = club, AwayClub = away, KickoffUtc = DateTimeOffset.UtcNow.AddDays(1) }] });
            await database.SaveChangesAsync();
        }
        var path = $"/api/competitions/{group.GroupId}/{league}/22/opening";
        Assert.Equal(HttpStatusCode.NoContent, (await owner.GetAsync(path + "/standings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.PostAsync(path + "/prepare", null)).StatusCode);
        var prepare = await owner.PostAsync(path + "/prepare", null);
        prepare.EnsureSuccessStatusCode();
        var standings = (await prepare.Content.ReadFromJsonAsync<OpeningStandingsDto>())!;
        Assert.Equal(teamId, Assert.Single(standings.Teams).TeamId);
        var roster = (await owner.GetFromJsonAsync<TeamRosterDto>(path + "/team?day=1"))!;
        Assert.Equal(22, roster.SeasonIndex);
        Assert.Equal(teamId, roster.TeamId);
        Assert.Equal(0, roster.Version);
        Assert.Equal(1, roster.SourceVersion);
        var request = new SaveRosterFormationRequest(group.GroupId, basket, 22, roster.Owner, 1, league, 3,
            roster.Players.Select(x => new FormationPositionRequest(x.PlayerId, x.Position)).ToArray(), roster.Version, TeamId: teamId, SourceVersion: roster.SourceVersion);
        (await owner.PutAsJsonAsync("/api/teams/formation", request)).EnsureSuccessStatusCode();
        (await owner.PostAsync(path + "/prepare", null)).EnsureSuccessStatusCode();
        (await owner.PostAsync(path + "/prepare", null)).EnsureSuccessStatusCode();
        await using var final = factory.Database.CreateContext();
        Assert.Equal(1, await final.OpeningStandingSnapshots.CountAsync(x => x.GroupId == group.GroupId));
        var saved = await final.RosterSnapshots.SingleAsync(x => x.GroupId == group.GroupId && x.SeasonIndex == 22 && x.Kind == RosterKind.OpeningMatchday);
        Assert.Equal(21, saved.TeamOriginYear);
        Assert.All(saved.Players, x => Assert.Equal(10, x.Price));
        Assert.Equal(11, await final.PlayerSeasons.CountAsync(x => x.SeasonIndex == 22));
        var updated = await final.Players.SingleAsync(x => x.Name == "Updated opening display label");
        Assert.NotEqual(KeyUtils.GetPlayerKey(updated.Name), updated.Id);
        Assert.Equal(new[] { 21, 22 }, await final.PlayerSeasons.Where(x => x.PlayerId == updated.Id).OrderBy(x => x.SeasonIndex).Select(x => x.SeasonIndex).ToArrayAsync());
    }
}
