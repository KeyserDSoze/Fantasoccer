using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Football;
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
    public async Task Game_HTTP_uses_SQL_scopes_saved_scores_natural_player_keys_and_live_limits_without_client_financial_data()
    {
        using var owner = await factory.AccountAsync(globalAdmin: true);
        using var opponent = await factory.AccountAsync();
        using var outsider = await factory.AccountAsync();
        var group = await CreateGroupAsync(owner);
        var league = await CreateLeagueAsync(owner, group.GroupId, 18);
        var first = (await owner.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var second = (await opponent.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        (await owner.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(second.Email, "Opponent", 2))).EnsureSuccessStatusCode();
        var graph = (await owner.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var basket = graph.Baskets[0].Id;
        foreach (var account in new[] { first, second })
            (await owner.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, 18, account.Id == first.Id ? "Home" : "Away", account.Id, []))).EnsureSuccessStatusCode();
        Guid home, away, game;
        await using (var database = factory.Database.CreateContext())
        {
            var rosters = await database.RosterSnapshots.Where(x => x.GroupId == group.GroupId).ToArrayAsync();
            home = rosters.Single(x => x.OwnerUserId == first.Id).TeamId;
            away = rosters.Single(x => x.OwnerUserId == second.Id).TeamId;
            var club = new FootballClub { Name = "Game club", Abbreviation = "gme" };
            var other = new FootballClub { Name = "Game other club", Abbreviation = "oth" };
            database.Clubs.AddRange(club, other);
            foreach (var roster in rosters)
            {
                var roles = new[] { 0, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 1 };
                for (var i = 0; i < roles.Length; i++)
                {
                    var player = new FootballPlayer { Name = "Game player " + Guid.NewGuid().ToString("N") };
                    database.Players.Add(player);
                    database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = 18, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Club = club, Position = (PlayerPosition)roles[i] });
                    roster.Players.Add(new() { Ordinal = i, Player = player, Club = club, PlayerRole = roles[i], Status = 0,
                        Price = i + 10, Position = i == 11 ? 5 : roles[i], IsActive = true, IsVisible = true });
                }
            }
            var rules = await database.LeagueRules.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == league && x.SeasonIndex == 18);
            rules.LiveFormationChanges = 1;
            database.FootballMatchdays.Add(new() { SeasonIndex = 18, SerieADay = 1, Matches =
                [new() { SeasonIndex = 18, SerieADay = 1, Ordinal = 0, HomeClub = club, AwayClub = other, KickoffUtc = DateTimeOffset.UtcNow.AddDays(1) }] });
            game = Guid.NewGuid();
            database.FantasyCalendars.Add(new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = 18, Stages =
                [new() { Stage = "Final stage", GroupId = group.GroupId, LeagueId = league, SeasonIndex = 18, Days =
                    [new() { Stage = "Final stage", GroupId = group.GroupId, LeagueId = league, SeasonIndex = 18, Ordinal = 0, FantasyTurn = 8, SerieADay = 1, Games =
                        [new() { GroupId = group.GroupId, LeagueId = league, SeasonIndex = 18, Id = game, Stage = "Final stage", DayOrdinal = 0,
                            HomeTeamId = home, HomeBasketId = basket, AwayTeamId = away, AwayBasketId = basket, HomeName = "Saved home", AwayName = "Saved away", HomeOwner = first.Email, AwayOwner = second.Email,
                            Result = new() { Home = new() { Value = 88.5f }, Away = new() { Value = 64 }, HomeGoals = 4, AwayGoals = 0 } }] }] }] });
            await database.SaveChangesAsync();
        }
        var path = $"/api/competitions/{group.GroupId}/{league}/18/games/{game}";
        var editable = (await owner.GetFromJsonAsync<GameDto>(path + "?editMode=formation"))!;
        Assert.True(editable.CanEdit);
        Assert.True(editable.Teams[0].CanEdit);
        Assert.False(editable.Teams[1].CanEdit);
        Assert.Equal(88.5f, editable.Game.Result!.Home.Value);
        Assert.False(editable.ResultIsPreview);
        Assert.Equal(0, editable.Teams[0].Version);
        Assert.Equal(1, editable.Teams[0].SourceVersion);
        var positions = editable.Teams[0].Players.Select(x => new FormationPositionRequest(x.Current.PlayerId, x.Current.Position)).ToArray();
        var request = new SaveGameFormationRequest(home, positions, 0, 1);
        var response = await owner.PutAsJsonAsync(path + "/formation", request);
        response.EnsureSuccessStatusCode();
        var stored = (await response.Content.ReadFromJsonAsync<FormationSavedDto>())!;
        Assert.Equal(1, stored.Team.Version);
        Assert.All(stored.Team.Players, player =>
        {
            Assert.Equal(18, player.SeasonIndex); Assert.Equal(KeyUtils.GetPlayerKey(player.Name), player.PlayerKey);
        });
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync(path + "/formation", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await opponent.PutAsJsonAsync(path + "/formation", request with { ExpectedVersion = 1 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path + "/formation", request with { TeamId = Guid.NewGuid() })).StatusCode);
        var optimal = await owner.GetAsync($"/api/competitions/{group.GroupId}/{league}/18/optimal-formation?day=1&owner={Uri.EscapeDataString(first.Email)}");
        optimal.EnsureSuccessStatusCode();
        Assert.All((await optimal.Content.ReadFromJsonAsync<OptimalGameFormationDto>())!.Players, player => Assert.False(string.IsNullOrWhiteSpace(player.PlayerId)));
        await RosterSnapshotFixtures.FreezeAsync(factory.Database, group.GroupId, 18, 1);
        await using (var database = factory.Database.CreateContext())
        {
            var match = await database.FootballMatches.SingleAsync(x => x.SeasonIndex == 18 && x.SerieADay == 1);
            match.KickoffUtc = DateTimeOffset.UtcNow.AddHours(-1);
            await database.SaveChangesAsync();
        }
        (positions[1], positions[11]) = (positions[1] with { Position = 5 }, positions[11] with { Position = 1 });
        var live = request with { Positions = positions, ExpectedVersion = 1, SourceVersion = null, Live = true };
        (await owner.PutAsJsonAsync(path + "/formation", live)).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync(path + "/formation", live with { ExpectedVersion = 2 })).StatusCode);
        var final = (await owner.GetFromJsonAsync<GameDto>(path))!;
        Assert.Equal(0, final.Teams[0].FormationChangesRemaining);
        Assert.Equal(88.5f, final.Game.Result!.Home.Value);
        Assert.Equal(editable.Teams[0].Players.OrderBy(x => x.Current.PlayerId).Select(x => x.Current.Price),
            final.Teams[0].Players.OrderBy(x => x.Current.PlayerId).Select(x => x.Current.Price));
    }

    [PostgreSqlFact]
    public async Task Football_HTTP_returns_readable_shared_data_and_only_trusted_global_admins_can_change_the_schedule()
    {
        using var admin = await factory.AccountAsync(globalAdmin: true);
        using var reader = await factory.AccountAsync();
        var group = await CreateGroupAsync(admin);
        await CreateLeagueAsync(admin, group.GroupId, 19);
        await using (var database = factory.Database.CreateContext())
        {
            var club = new FootballClub { Name = "Shared HTTP club", Abbreviation = "shc" };
            var away = new FootballClub { Name = "Shared HTTP away", Abbreviation = "sha" };
            var player = new FootballPlayer { Name = "Shared HTTP player" };
            database.Clubs.AddRange(club, away); database.Players.Add(player);
            if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == 20)) database.Seasons.Add(new() { SeasonIndex = 20, StartYear = 2031 });
            database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = 19, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Club = club, Position = PlayerPosition.Forward, IsActive = true });
            database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = 20, Club = away, Position = PlayerPosition.Forward, IsActive = true });
            database.FootballVotes.Add(new() { Player = player, Club = club, SeasonIndex = 19, Day = 2, Source = FootballVoteSource.Official,
                PlayerRole = 3, VoteRole = 3, Value = 7.5f, HasVote = true, Goals = 1 });
            database.FootballMatchdays.Add(new() { SeasonIndex = 19, SerieADay = 2, Matches =
                [new() { SeasonIndex = 19, SerieADay = 2, Ordinal = 0, HomeClub = club, AwayClub = away, KickoffUtc = new(2030, 10, 27, 19, 45, 0, TimeSpan.Zero) }] });
            database.FootballDatasets.Add(new() { Kind = "official", SeasonIndex = 19, Day = 3 }); // Present but empty, not missing.
            await database.SaveChangesAsync();
        }
        var json = await reader.GetFromJsonAsync<JsonElement>("/api/football/19/days/2/votes/official");
        var voted = json.GetProperty("players")[0];
        Assert.True(voted.TryGetProperty("playerId", out _));
        Assert.Equal(19, voted.GetProperty("seasonIndex").GetInt32());
        Assert.Equal("sharedhttpplayer", voted.GetProperty("playerKey").GetString());
        var found = (await reader.GetFromJsonAsync<FootballPlayerDto>("/api/football/19/players/by-key/sharedhttpplayer"))!;
        Assert.Equal(voted.GetProperty("playerId").GetString(), found.PlayerId); Assert.Equal("sharedhttpplayer", found.PlayerKey);
        Assert.Equal("shc", found.RealTeam!.ClubCode); Assert.Equal(found.RealTeam.ClubId, found.RealTeam.ClubCode);
        var history = (await reader.GetFromJsonAsync<PlayerHistoryDto>("/api/football/players/by-key/sharedhttpplayer"))!;
        Assert.Equal("sharedhttpplayer", history.PlayerKey); Assert.Equal(new[] { 19, 20 }, history.Years);
        var nextYear = (await reader.GetFromJsonAsync<FootballPlayerDto>("/api/football/20/players/by-key/sharedhttpplayer"))!;
        Assert.Equal(found.PlayerKey, nextYear.PlayerKey); Assert.Equal(20, nextYear.SeasonIndex); Assert.Equal("sha", nextYear.RealTeam!.ClubCode);
        Assert.Equal(HttpStatusCode.NoContent, (await reader.GetAsync("/api/football/players/by-key/unknownplayer")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reader.GetAsync("/api/football/players/by-key/not-a-canonical-key")).StatusCode);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/football/players/by-key/sharedhttpplayer")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reader.GetAsync("/api/football/19/players/by-key/18%3Asharedhttpplayer")).StatusCode);
        Assert.Equal(7.5f, voted.GetProperty("vote").GetProperty("value").GetSingle());
        Assert.False(voted.TryGetProperty("v", out _));
        Assert.Empty((await reader.GetFromJsonAsync<PlayersDto<VotedPlayerDto>>("/api/football/19/days/3/votes/official"))!.Players);
        Assert.Equal(HttpStatusCode.NoContent, (await reader.GetAsync("/api/football/19/days/4/votes/official")).StatusCode);
        var path = "/api/football/19/days/2/games/0/delay";
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PatchAsJsonAsync(path, new DelayFootballGameRequest(true, false))).StatusCode);
        (await admin.PatchAsJsonAsync(path, new DelayFootballGameRequest(true, false))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PatchAsJsonAsync(path, new DelayFootballGameRequest(false, false))).StatusCode);
        var calendar = (await reader.GetFromJsonAsync<FootballCalendarDto>("/api/football/19/calendar"))!;
        Assert.True(calendar.Days[0].Games[0].Delayed);
        Assert.Equal(TimeSpan.Zero, calendar.Days[0].Games[0].Date!.Value.Offset);
    }
}
