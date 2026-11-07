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
        // A second team of the same league owns the keeper too: both must be shown.
        using var guest = await factory.AccountAsync();
        var guestIdentity = (await guest.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        (await manager.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(guestIdentity.Email, "Guest", 2))).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, "Second basket team", guestIdentity.Id, []))).EnsureSuccessStatusCode();

        var kickoff = DateTimeOffset.UtcNow.AddHours(-30);
        var home = new FootballClub { Name = "Match home", Abbreviation = "mxa" };
        var away = new FootballClub { Name = "Match away", Abbreviation = "mxb" };
        var other = new FootballClub { Name = "Match other", Abbreviation = "mxc" };
        FootballPlayer Make(string name) => new() { Name = name + " " + Guid.NewGuid().ToString("N") };
        var keeper = Make("Mx keeper"); var defender = Make("Mx defender"); var substitute = Make("Mx substitute"); var called = Make("Mx called");
        var injured = Make("Mx injured"); var regular = Make("Mx regular"); var leaver = Make("Mx leaver"); var arrival = Make("Mx arrival"); var awayKeeper = Make("Mx away keeper");
        await using (var database = factory.Database.CreateContext())
        {
            database.Clubs.AddRange(home, away, other);
            database.Players.AddRange(injured, keeper, defender, substitute, called, regular, leaver, arrival, awayKeeper);
            void Season(FootballPlayer player, FootballClub? club, PlayerPosition position, FootballClub? previous = null, DateTimeOffset? moved = null) =>
                database.PlayerSeasons.Add(new() { Player = player, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Club = club, PreviousClub = previous,
                    TransferredAtUtc = moved, Position = position, IsActive = true, IsVisible = true });
            Season(keeper, home, PlayerPosition.Goalkeeper); Season(defender, home, PlayerPosition.Defender); Season(substitute, home, PlayerPosition.Forward);
            Season(injured, home, PlayerPosition.Forward);
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
            var rosters = await database.RosterSnapshots.Include(x => x.Players).Where(x => x.GroupId == group.GroupId && x.Kind == RosterKind.Season).ToArrayAsync();
            foreach (var roster in rosters)
            {
                roster.Players.Add(new() { Ordinal = 0, PlayerId = KeyUtils.GetPlayerKey(keeper.Name), ClubId = home.Id, PlayerRole = 0, IsActive = true, IsVisible = true, Price = 5, Position = 0 });
                if (roster.OwnerUserId == me.Id)
                    roster.Players.Add(new() { Ordinal = 1, PlayerId = KeyUtils.GetPlayerKey(substitute.Name), ClubId = home.Id, PlayerRole = 3, IsActive = true, IsVisible = true, Price = 5, Position = 11 });
            }
            await database.SaveChangesAsync();
        }

        // The day's availability data (injured / disqualified) is shown while the snapshot is still usable.
        await factory.Services.GetRequiredService<IFootballLiveDataStore>().PublishChancesAsync(DateTimeOffset.UtcNow, new(year, day, ["fantagazzetta"],
            [new CachedChance(KeyUtils.GetPlayerKey(injured.Name), injured.Name, "mxa", "Match home", 3, true, true, false, false, false, false, 1, "Distorsione")]));
        // The official page does not say who came on (it marks everybody as in): the live feed does.
        await factory.Services.GetRequiredService<IFootballLiveDataStore>().PublishLiveVotesAsync(DateTimeOffset.UtcNow, new(year, day,
            [new CachedLiveVote(KeyUtils.GetPlayerKey(substitute.Name), substitute.Name, home.Id, home.Name, 3, true, true,
                new CachedVote(3, 6, false, 0, 0, 0, 0, 0, 0, 0, 0, false, true, false, true, false))]), null, default);
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
        Assert.Equal((LineupKinds.Stand, 1), (Find(homeSide, injured).LineupKind!, Find(homeSide, injured).Chance!.Status));
        Assert.Single(awaySide.Players);
        Assert.All(homeSide.Players, x => Assert.Null(x.FantasyTeams));

        var withLeague = (await manager.GetFromJsonAsync<FootballMatchDto>($"{path}?groupId={group.GroupId}&leagueId={league}"))!;
        Assert.Equal("League", withLeague.FantasyLeagueName);
        Assert.Equal(["Overlay team", "Second basket team"], Find(withLeague.Lineup.Teams[0], keeper).FantasyTeams!.Select(x => x.Name).Order().ToArray());
        Assert.Equal(["Overlay team"], Find(withLeague.Lineup.Teams[0], substitute).FantasyTeams!.Select(x => x.Name).ToArray());
        Assert.Null(Find(withLeague.Lineup.Teams[0], defender).FantasyTeams);

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
    [PostgreSqlFact]
    public async Task Official_votes_mark_every_player_as_in_yet_the_pitch_gets_its_eleven_from_the_live_feed_or_from_who_played_most()
    {
        const int year = 38; const int day = 20;
        using var manager = await factory.AccountAsync(globalAdmin: true);
        var home = new FootballClub { Name = "Pitch home", Abbreviation = "pha" };
        var away = new FootballClub { Name = "Pitch away", Abbreviation = "phb" };
        var players = Enumerable.Range(0, 15).Select(i => new FootballPlayer { Name = $"Pitch player {i:00} {Guid.NewGuid():N}" }).ToArray();
        var role = (int i) => i == 0 ? 0 : 1 + i % 3;
        await using (var database = factory.Database.CreateContext())
        {
            if (!await database.Seasons.AnyAsync(x => x.SeasonIndex == year)) database.Seasons.Add(new() { SeasonIndex = year, StartYear = year + 2011 });
            database.Clubs.AddRange(home, away); database.Players.AddRange(players);
            for (var i = 0; i < players.Length; i++)
            {
                database.PlayerSeasons.Add(new() { Player = players[i], SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(players[i].Name), Club = home,
                    Position = (PlayerPosition)role(i), IsActive = true, IsVisible = true });
                // The official page: everyone on it is "in", all have a vote.
                database.FootballVotes.Add(new() { Player = players[i], Club = home, SeasonIndex = year, Day = day, Source = FootballVoteSource.Official, PlayerRole = role(i), VoteRole = role(i),
                    Value = 6, HasVote = true, IsIn = true, IsFinal = true, IsActive = true, IsVisible = true });
                // Earlier appearances: the lower the number, the more he played.
                for (var earlier = 1; earlier <= 14 - i; earlier++)
                    database.FootballVotes.Add(new() { Player = players[i], Club = home, SeasonIndex = year, Day = day - earlier, Source = FootballVoteSource.Official, PlayerRole = role(i), VoteRole = role(i),
                        Value = 6, HasVote = true, IsFinal = true, IsActive = true, IsVisible = true });
            }
            database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = day, Matches =
                [new() { SeasonIndex = year, SerieADay = day, Ordinal = 0, HomeClub = home, AwayClub = away, KickoffUtc = DateTimeOffset.UtcNow.AddHours(-30), HomeGoals = 1, AwayGoals = 1, IsFinished = true }] });
            await database.SaveChangesAsync();
        }
        var path = $"/api/football/{year}/days/{day}/matches/0";
        Dictionary<string, int> Positions(FootballMatchDto match) => match.Lineup.Teams[0].Players.ToDictionary(x => x.Current.PlayerId, x => x.CurrentPosition);
        string Key(int i) => KeyUtils.GetPlayerKey(players[i].Name);

        // No live feed any more: the keeper and the ten outfield players who played most (the first ones) start, the rest is called up.
        var heuristic = Positions((await manager.GetFromJsonAsync<FootballMatchDto>(path))!);
        Assert.Equal(11, heuristic.Values.Count(x => x <= 3));
        Assert.Equal(0, heuristic[Key(0)]);
        for (var i = 1; i <= 10; i++) Assert.True(heuristic[Key(i)] <= 3, $"player {i} starts");
        for (var i = 11; i < 15; i++) Assert.True(heuristic[Key(i)] is >= 4 and <= 10, $"player {i} is on the bench");

        // The live feed still has the day: it says who really started (player 12 instead of 3) and who came on.
        CachedVote Flags(bool isIn, bool isOut) => new(1, 6, false, 0, 0, 0, 0, 0, 0, 0, 0, false, true, isOut, isIn, false);
        var starters = new[] { 0, 1, 2, 12, 4, 5, 6, 7, 8, 9, 10 };
        await factory.Services.GetRequiredService<IFootballLiveDataStore>().PublishLiveVotesAsync(DateTimeOffset.UtcNow, new(year, day,
            Enumerable.Range(0, 15).Select(i => new CachedLiveVote(Key(i), players[i].Name, home.Id, home.Name, role(i), true, true, Flags(!starters.Contains(i), false))).ToArray()), null, default);
        factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>().Remove($"serie-a-match:{year}:{day}:0");
        var live = Positions((await manager.GetFromJsonAsync<FootballMatchDto>(path))!);
        Assert.Equal(11, live.Values.Count(x => x <= 3));
        Assert.True(live[Key(12)] <= 3); Assert.True(live[Key(3)] is >= 4 and <= 10);
        // Who came on is a substitute, not a starter.
        Assert.All(new[] { 3, 11, 13, 14 }, i => Assert.True(live[Key(i)] is >= 4 and <= 10, $"player {i}"));

        // Once the in/out flags are kept on the vote rows (real: not everybody is in) they are what counts, whatever the live feed says now.
        await using (var database = factory.Database.CreateContext())
        {
            foreach (var row in await database.FootballVotes.Where(x => x.SeasonIndex == year && x.Day == day).ToArrayAsync())
                row.IsIn = Array.FindIndex(players, x => KeyUtils.GetPlayerKey(x.Name) == row.PlayerId) >= 11;
            await database.SaveChangesAsync();
        }
        factory.Services.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>().Remove($"serie-a-match:{year}:{day}:0");
        var stored = Positions((await manager.GetFromJsonAsync<FootballMatchDto>(path))!);
        Assert.True(stored[Key(3)] <= 3); Assert.True(stored[Key(12)] is >= 4 and <= 10);
    }
}
