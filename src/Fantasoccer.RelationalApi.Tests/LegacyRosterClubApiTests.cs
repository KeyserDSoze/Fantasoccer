using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Market;
using Fantasoccer.Application.Core.Teams;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Legacy_roster_players_without_a_club_are_listed_with_an_empty_club_and_cannot_be_traded()
    {
        var year = FantacalcioTime.ActualYear; // the market only runs in the current season
        using var manager = await factory.AccountAsync(globalAdmin: true);
        using var participant = await factory.AccountAsync();
        var group = await CreateGroupAsync(manager);
        var league = await CreateLeagueAsync(manager, group.GroupId, year);
        var owner = (await manager.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var guest = (await participant.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        (await manager.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(guest.Email, "Guest", 2))).EnsureSuccessStatusCode();
        var basket = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!.Baskets[0].Id;
        var teams = new List<Guid>();
        foreach (var user in new[] { owner, guest })
        {
            var created = await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, "Legacy " + teams.Count, user.Id, []));
            created.EnsureSuccessStatusCode(); teams.Add((await created.Content.ReadFromJsonAsync<TeamCreatedDto>())!.TeamId);
        }
        string unknownClubPlayer, tradedPlayer;
        await using (var database = factory.Database.CreateContext())
        {
            (await database.Leagues.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == league)).IsMain = true;
            var club = new FootballClub { Name = "Legacy roster club", Abbreviation = "lrc" };
            database.Clubs.Add(club);
            FootballPlayer Add(FootballClub? annualClub)
            {
                var player = new FootballPlayer { Name = "Legacy roster player " + Guid.NewGuid().ToString("N") };
                database.Players.Add(player);
                database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Club = annualClub, Position = PlayerPosition.Forward });
                return player;
            }
            var unknown = Add(null); var traded = Add(club);
            unknownClubPlayer = unknown.Id; tradedPlayer = traded.Id;
            var rosters = await database.RosterSnapshots.Where(x => x.GroupId == group.GroupId && x.Kind == RosterKind.Season).ToArrayAsync();
            rosters.Single(x => x.TeamId == teams[0]).Players.Add(new() { Ordinal = 0, Player = unknown, Club = null, PlayerRole = 3, Price = 12, Position = 3, Status = 0 });
            rosters.Single(x => x.TeamId == teams[1]).Players.Add(new() { Ordinal = 0, Player = traded, Club = club, PlayerRole = 3, Price = 20, Position = 3, Status = 0 });
            await database.SaveChangesAsync();
        }

        // Same empty club the legacy document contained; no club is invented.
        var roster = (await manager.GetFromJsonAsync<TeamRosterDto>($"/api/teams/roster?groupId={group.GroupId}&basketId={basket}&year={year}&owner={Uri.EscapeDataString(owner.Email)}&kind=0&day=0"))!;
        var listed = roster.Players.Single(x => x.PlayerId == unknownClubPlayer);
        Assert.Equal(("", "", 12), (listed.Team.Name, listed.Team.Abbreviation, listed.Price));

        var other = (await manager.GetFromJsonAsync<TeamRosterDto>($"/api/teams/roster?groupId={group.GroupId}&basketId={basket}&year={year}&owner={Uri.EscapeDataString(guest.Email)}&kind=0&day=0"))!;
        var response = await manager.PostAsJsonAsync($"/api/competitions/{group.GroupId}/{league}/{year}/market",
            new CreateMarketRequest(teams[0], teams[1], [unknownClubPlayer], [tradedPlayer], 0, 0, roster.Version, other.Version));
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
        Assert.Contains("market_player_club_unknown", body);
        await using var final = factory.Database.CreateContext();
        Assert.False(await final.MarketTrades.AnyAsync(x => x.GroupId == group.GroupId));
        var saved = await final.RosterSnapshots.AsNoTracking().Include(x => x.Players).SingleAsync(x => x.TeamId == teams[0] && x.Kind == RosterKind.Season);
        Assert.Null(saved.Players.Single().ClubId);
    }
}
