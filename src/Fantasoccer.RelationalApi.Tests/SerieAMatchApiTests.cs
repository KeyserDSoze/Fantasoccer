using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Real_match_rebuilds_lineups_from_votes_and_transfers_overlays_the_fantasy_league_and_requires_group_access()
    {
        const int year = 38; const int day = 6;
        using var manager = await factory.AccountAsync(globalAdmin: true);
        using var outsider = await factory.AccountAsync();
        var group = await CreateGroupAsync(manager);
        var league = await CreateLeagueAsync(manager, group.GroupId, year);
        var me = (await manager.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var basket = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!.Baskets[0].Id;
        (await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, "Overlay team", me.Id, []))).EnsureSuccessStatusCode();

        var kickoff = DateTimeOffset.UtcNow.AddHours(-30);
        var home = new FootballClub { Name = "Match home", Abbreviation = "mxa" };
        var away = new FootballClub { Name = "Match away", Abbreviation = "mxb" };
        var other = new FootballClub { Name = "Match other", Abbreviation = "mxc" };
        FootballPlayer Make(string name) => new() { Name = name + " " + Guid.NewGuid().ToString("N") };
        var keeper = Make("Mx keeper"); var defender = Make("Mx defender"); var substitute = Make("Mx substitute"); var called = Make("Mx called");
        var regular = Make("Mx regular"); var leaver = Make("Mx leaver"); var arrival = Make("Mx arrival"); var awayKeeper = Make("Mx away keeper");
        await using (var database = factory.Database.CreateContext())
        {
            database.Clubs.AddRange(home, away, other);
            database.Players.AddRange(keeper, defender, substitute, called, regular, leaver, arrival, awayKeeper);
            void Season(FootballPlayer player, FootballClub? club, PlayerPosition position, FootballClub? previous = null, DateTimeOffset? moved = null) =>
                database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Club = club, PreviousClub = previous,
                    TransferredAtUtc = moved, Position = position, IsActive = true, IsVisible = true });
            Season(keeper, home, PlayerPosition.Goalkeeper); Season(defender, home, PlayerPosition.Defender); Season(substitute, home, PlayerPosition.Forward);
            Season(called, home, PlayerPosition.Midfielder); Season(regular, home, PlayerPosition.Defender);
            Season(leaver, null, PlayerPosition.Midfielder, home, DateTimeOffset.UtcNow);
            Season(arrival, home, PlayerPosition.Forward, other, DateTimeOffset.UtcNow);
            Season(awayKeeper, away, PlayerPosition.Goalkeeper);
            void Vote(FootballPlayer player, FootballClub club, int role, bool hasVote, bool isIn = false, int day = day, float value = 6f) =>
                database.FootballVotes.Add(new() { Player = player, Club = club, SeasonIndex = year, Day = day, Source = FootballVoteSource.Official, PlayerRole = role, VoteRole = role,
                    Value = hasVote ? value : 0, HasVote = hasVote, IsIn = isIn, IsFinal = true, IsActive = true, IsVisible = true });
            Vote(keeper, home, 0, true); Vote(defender, home, 1, true, value: 7f); Vote(substitute, home, 3, true, isIn: true); Vote(called, home, 2, false);
            Vote(awayKeeper, away, 0, true);
            Vote(regular, home, 1, true, day: day - 1); // an earlier appearance, so he leads the estimated bench
            database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = day, Matches =
                [new() { SeasonIndex = year, SerieADay = day, Ordinal = 0, HomeClub = home, AwayClub = away, KickoffUtc = kickoff, HomeGoals = 2, AwayGoals = 0, IsFinished = true }] });
            var roster = await database.RosterSnapshots.Include(x => x.Players).SingleAsync(x => x.GroupId == group.GroupId && x.Kind == RosterKind.Season);
            roster.Players.Add(new() { Ordinal = 0, PlayerId = KeyUtils.GetPlayerKey(keeper.Name), ClubId = home.Id, PlayerRole = 0, IsActive = true, IsVisible = true, Price = 5, Position = 0 });
            await database.SaveChangesAsync();
        }

        var path = $"/api/football/{year}/days/{day}/matches/0";
        var match = (await manager.GetFromJsonAsync<FootballMatchDto>(path))!;
        Assert.Equal(LineupSources.Official, match.HomeLineupSource); Assert.Equal(LineupSources.Official, match.AwayLineupSource);
        Assert.False(match.IsPlaying);
        var homeSide = match.Lineup.Teams[0]; var awaySide = match.Lineup.Teams[1];
        Assert.Equal("Match home", homeSide.Name); Assert.False(homeSide.CanEdit);
        LivePlayerDto Find(LiveTeamDto side, FootballPlayer player) => side.Players.Single(x => x.Current.PlayerId == KeyUtils.GetPlayerKey(player.Name));
        Assert.Equal((0, LineupKinds.Starter), (Find(homeSide, keeper).CurrentPosition, Find(homeSide, keeper).LineupKind!));
        Assert.Equal((1, LineupKinds.Starter), (Find(homeSide, defender).CurrentPosition, Find(homeSide, defender).LineupKind!));
        Assert.Equal(7f, Find(homeSide, defender).FinalValue!.Value);
        Assert.Equal((9, LineupKinds.Substitute), (Find(homeSide, substitute).CurrentPosition, Find(homeSide, substitute).LineupKind!));
        Assert.Equal((7, LineupKinds.Bench), (Find(homeSide, called).CurrentPosition, Find(homeSide, called).LineupKind!));
        Assert.Equal((5, LineupKinds.EstimatedBench), (Find(homeSide, regular).CurrentPosition, Find(homeSide, regular).LineupKind!));
        // He left after kickoff: still part of the club that day. The player who arrived after kickoff was not.
        Assert.Equal(LineupKinds.EstimatedBench, Find(homeSide, leaver).LineupKind);
        Assert.DoesNotContain(homeSide.Players, x => x.Current.PlayerId == KeyUtils.GetPlayerKey(arrival.Name));
        Assert.Single(awaySide.Players);
        Assert.All(homeSide.Players, x => Assert.Null(x.FantasyTeam));

        var withLeague = (await manager.GetFromJsonAsync<FootballMatchDto>($"{path}?groupId={group.GroupId}&leagueId={league}"))!;
        Assert.Equal("League", withLeague.FantasyLeagueName);
        Assert.Equal("Overlay team", Find(withLeague.Lineup.Teams[0], keeper).FantasyTeam?.Name);
        Assert.Null(Find(withLeague.Lineup.Teams[0], defender).FantasyTeam);

        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"{path}?groupId={group.GroupId}&leagueId={league}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"{path}?groupId={group.GroupId}&leagueId={Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await manager.GetAsync($"/api/football/{year}/days/{day}/matches/9")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await outsider.GetAsync(path)).StatusCode);
    }

    [PostgreSqlFact]
    public async Task Real_match_before_kickoff_shows_the_probable_lineup_and_without_data_is_unavailable()
    {
        const int year = 39; const int day = 2;
        using var manager = await factory.AccountAsync(globalAdmin: true);
        var kickoff = DateTimeOffset.UtcNow.AddDays(3);
        var home = new FootballClub { Name = "Probable home", Abbreviation = "pxa" };
        var away = new FootballClub { Name = "Probable away", Abbreviation = "pxb" };
        var players = Enumerable.Range(0, 12).Select(i => new FootballPlayer { Name = $"Probable player {i} {Guid.NewGuid():N}" }).ToArray();
        await using (var database = factory.Database.CreateContext())
        {
            if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == year)) database.Seasons.Add(new() { SeasonIndex = year, StartYear = 2011 + year });
            database.Clubs.AddRange(home, away); database.Players.AddRange(players);
            foreach (var (player, index) in players.Select((p, i) => (p, i)))
                database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Club = home,
                    Position = index == 0 ? PlayerPosition.Goalkeeper : (PlayerPosition)(1 + index % 3), IsActive = true, IsVisible = true });
            database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = day, Matches =
                [new() { SeasonIndex = year, SerieADay = day, Ordinal = 0, HomeClub = home, AwayClub = away, KickoffUtc = kickoff }] });
            await database.SaveChangesAsync();
        }
        var path = $"/api/football/{year}/days/{day}/matches/0";
        var none = (await manager.GetFromJsonAsync<FootballMatchDto>(path))!;
        Assert.Equal(LineupSources.Unavailable, none.HomeLineupSource); Assert.Empty(none.Lineup.Teams[0].Players);

        var chances = players.Take(11).Select((p, i) => new CachedChance(KeyUtils.GetPlayerKey(p.Name), p.Name, "pxa", "Probable home", i == 0 ? 0 : 1 + i % 3, true, true, true, i % 2 == 0, false, false, 0, null)).ToArray();
        await factory.Services.GetRequiredService<IFootballLiveDataStore>().PublishChancesAsync(DateTimeOffset.UtcNow, new(year, day, ["fantagazzetta"], chances));
        factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>().Remove($"serie-a-match:{year}:{day}:0");
        var probable = (await manager.GetFromJsonAsync<FootballMatchDto>(path))!;
        Assert.Equal(LineupSources.Probable, probable.HomeLineupSource); Assert.Equal(LineupSources.Unavailable, probable.AwayLineupSource);
        var starters = probable.Lineup.Teams[0].Players.Where(x => x.CurrentPosition <= 3).ToArray();
        Assert.Equal(11, starters.Length); Assert.All(starters, x => Assert.Equal(LineupKinds.EstimatedStarter, x.LineupKind));
        Assert.Equal(1, starters.Count(x => x.CurrentPosition == 0));
        Assert.Contains(probable.Lineup.Teams[0].Players, x => x.LineupKind == LineupKinds.EstimatedBench);
        Assert.Equal(12, probable.Lineup.Teams[0].Players.Length);
    }
}
