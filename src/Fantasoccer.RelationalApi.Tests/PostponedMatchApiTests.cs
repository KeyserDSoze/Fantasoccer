using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Application.Core.Operations;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Postponed_games_use_role_political_votes_or_wait_for_actual_votes_per_league_and_the_recovery_job_closes_only_waiting_days()
    {
        const int year = 36; const int day = 4;
        using var manager = await factory.AccountAsync(globalAdmin: true);
        using var participant = await factory.AccountAsync();
        var group = await CreateGroupAsync(manager);
        var standard = await CreateLeagueAsync(manager, group.GroupId, year);
        var owner = (await manager.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var guest = (await participant.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        (await manager.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(guest.Email, "Guest", 2))).EnsureSuccessStatusCode();
        var basket = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!.Baskets[0].Id;
        foreach (var user in new[] { owner, guest })
            (await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, user.Id == owner.Id ? "Postponed" : "Regular", user.Id, []))).EnsureSuccessStatusCode();
        var created = await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/leagues", new CreateLeagueRequest("Waiting league", year, [basket]));
        created.EnsureSuccessStatusCode();
        var waiting = (await created.Content.ReadFromJsonAsync<LeagueCreatedDto>())!.LeagueId;

        var graph = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        LeagueSettingDto Settings(Guid league) => graph.Leagues.Single(x => x.Id == league).Years.Single(x => x.Year == year).Settings;
        Settings(standard).Postponements = new(false, 10.5f, 6, 6, 6);
        Assert.Equal(HttpStatusCode.BadRequest, (await manager.PutAsJsonAsync($"/api/groups/{group.GroupId}", graph)).StatusCode);
        Settings(standard).Postponements = new(false, 7, 5.5f, 6, 6);
        Settings(waiting).Postponements = new(true, 9, 9, 9, 9);
        (await manager.PutAsJsonAsync($"/api/groups/{group.GroupId}", graph)).EnsureSuccessStatusCode();
        var saved = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        Assert.Equal(new PostponementSettingsDto(false, 7, 5.5f, 6, 6), saved.Leagues.Single(x => x.Id == standard).Years.Single().Settings.Postponements);
        Assert.True(saved.Leagues.Single(x => x.Id == waiting).Years.Single().Settings.Postponements.WaitForRecovery);
        foreach (var league in new[] { standard, waiting })
            (await manager.PostAsJsonAsync($"/api/competitions/{group.GroupId}/{league}/{year}/calendar/generate", new GenerateCompetitionRequest())).EnsureSuccessStatusCode();

        Guid postponedTeam, regularTeam; string postponedKeeper;
        var clubs = new[] { "pda", "pdb", "pdc", "pdd" }.Select(code => new FootballClub { Name = "Postponed fixture " + code, Abbreviation = code }).ToArray();
        await using (var database = factory.Database.CreateContext())
        {
            database.Clubs.AddRange(clubs);
            // Both leagues share the basket; formation rules come from the main league.
            (await database.Leagues.SingleAsync(x => x.GroupId == group.GroupId && x.LeagueId == standard)).IsMain = true;
            var rosters = await database.RosterSnapshots.Where(x => x.GroupId == group.GroupId && x.Kind == RosterKind.Season).ToArrayAsync();
            postponedTeam = rosters.Single(x => x.OwnerUserId == owner.Id).TeamId; regularTeam = rosters.Single(x => x.OwnerUserId == guest.Id).TeamId;
            FootballPlayer Keeper(FootballClub club)
            {
                var player = new FootballPlayer { Name = "Postponed keeper " + club.Abbreviation + " " + Guid.NewGuid().ToString("N") };
                database.Players.Add(player);
                database.PlayerSeasons.Add(new() { Player = player, Club = club, SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(player.Name), Position = PlayerPosition.Goalkeeper });
                return player;
            }
            var delayed = Keeper(clubs[2]); var playing = Keeper(clubs[0]); var opponent = Keeper(clubs[1]);
            postponedKeeper = KeyUtils.GetPlayerKey(delayed.Name);
            foreach (var (roster, player, club) in new[] { (rosters.Single(x => x.TeamId == postponedTeam), delayed, clubs[2]), (rosters.Single(x => x.TeamId == regularTeam), playing, clubs[0]) })
                database.RosterSnapshots.Add(new() { GroupId = group.GroupId, BasketId = basket, SeasonIndex = year, TeamOriginYear = year, TeamId = roster.TeamId,
                    Kind = RosterKind.Matchday, Day = day, CompetitionId = Guid.Empty, Name = roster.Name, OwnerUserId = roster.OwnerUserId,
                    Players = [new() { Ordinal = 0, Player = player, Club = club, PlayerRole = 0, Position = 0, Status = 0, Price = 44 }] });
            foreach (var (player, club, value, suffered) in new[] { (playing, clubs[0], 6.5f, 1), (opponent, clubs[1], 6f, 0) })
                database.FootballVotes.Add(new() { Player = player, Club = club, SeasonIndex = year, Day = day, Source = FootballVoteSource.Official,
                    Value = value, SufferedGoals = suffered, PlayerRole = 0, VoteRole = 0, HasVote = true, IsFinal = true, IsIn = true });
            var original = DateTimeOffset.UtcNow.AddDays(-1);
            database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = day, Matches =
            [
                new() { SeasonIndex = year, SerieADay = day, Ordinal = 0, HomeClub = clubs[0], AwayClub = clubs[1], KickoffUtc = original, OriginalKickoffUtc = original, IsFinished = true },
                new() { SeasonIndex = year, SerieADay = day, Ordinal = 1, HomeClub = clubs[2], AwayClub = clubs[3], KickoffUtc = null, OriginalKickoffUtc = original,
                    Delayed = true, WasPostponed = true, ActualOfficialVotesReady = false }
            ] });
            await database.SaveChangesAsync();
        }

        async Task<RecalculationOutcome> Recalculate(Guid league)
        {
            var calendar = (await manager.GetFromJsonAsync<FantasyCalendarDto>($"/api/competitions/{group.GroupId}/{league}/{year}/calendar"))!;
            var response = await manager.PostAsJsonAsync($"/api/competitions/{group.GroupId}/{league}/{year}/recalculate", new RecalculateCompetitionRequest(day, PreciseDay: true, ExpectedVersion: calendar.Version));
            response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<RecalculationOutcome>())!;
        }
        async Task<(float Postponed, float Regular, CalendarDayDto Day)> Points(Guid league)
        {
            var calendar = (await manager.GetFromJsonAsync<FantasyCalendarDto>($"/api/competitions/{group.GroupId}/{league}/{year}/calendar"))!;
            var matchday = calendar.Rounds.SelectMany(x => x.Value).Single(x => x.SerieADay == day);
            var game = Assert.Single(matchday.Games);
            if (game.Result is null) return (float.NaN, float.NaN, matchday);
            return game.HomeTeamId == postponedTeam ? (game.Result.Home.Value, game.Result.Away.Value, matchday) : (game.Result.Away.Value, game.Result.Home.Value, matchday);
        }

        // Default: the political goalkeeper grade replaces the whole club at the
        // original deadline. It is flat: no clean sheet; fair-play still applies.
        Assert.Equal(new RecalculationOutcome(true, false), await Recalculate(standard));
        var points = await Points(standard);
        Assert.Equal(7 + 2, points.Postponed); Assert.Equal(6.5f - 1 + 2, points.Regular); Assert.False(points.Day.AwaitingPostponedMatches);
        var standardGame = (await manager.GetFromJsonAsync<FantasyCalendarDto>($"/api/competitions/{group.GroupId}/{standard}/{year}/calendar"))!
            .Rounds.SelectMany(x => x.Value).Single(x => x.SerieADay == day).Games.Single().Id;
        async Task<GameDto> Game(Guid league, Guid game)
        {
            var response = await manager.GetAsync($"/api/competitions/{group.GroupId}/{league}/{year}/games/{game}");
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
            return (await response.Content.ReadFromJsonAsync<GameDto>())!;
        }
        var view = await Game(standard, standardGame);
        var political = view.Teams.Single(x => x.TeamId == postponedTeam).Players.Single().Vote!;
        Assert.True(political.IsPolitical); Assert.Equal(7, political.Value); Assert.False(political.HasDoneSomething);

        // Waiting: nobody receives a political grade; the day remains open.
        Assert.Equal(new RecalculationOutcome(true, true), await Recalculate(waiting));
        points = await Points(waiting);
        Assert.True(float.IsNaN(points.Postponed)); Assert.True(points.Day.AwaitingPostponedMatches); Assert.False(points.Day.IsClosed);
        var job = await RunPostponedJobAsync();
        Assert.Equal(0, job.Processed); Assert.True(job.Skipped >= 1);
        Assert.True(float.IsNaN((await Points(waiting)).Postponed));

        // Recovery played much later: only actual official votes are stored.
        await using (var database = factory.Database.CreateContext())
        {
            var match = await database.FootballMatches.SingleAsync(x => x.SeasonIndex == year && x.SerieADay == day && x.Ordinal == 1);
            match.Delayed = false; match.KickoffUtc = DateTimeOffset.UtcNow.AddHours(-3); match.IsFinished = true; match.ActualOfficialVotesReady = true;
            var delayed = await database.Players.SingleAsync(x => x.Id == postponedKeeper);
            var opponent = new FootballPlayer { Name = "Recovery opponent " + Guid.NewGuid().ToString("N") };
            database.Players.Add(opponent);
            database.PlayerSeasons.Add(new() { Player = opponent, Club = await database.Clubs.SingleAsync(x => x.Id == "pdd"), SeasonIndex = year, NormalizedName = KeyUtils.GetPlayerKey(opponent.Name), Position = PlayerPosition.Goalkeeper });
            foreach (var (player, code, value, suffered) in new[] { (delayed, "pdc", 5f, 3), (opponent, "pdd", 6f, 0) })
                database.FootballVotes.Add(new() { Player = player, ClubId = code, SeasonIndex = year, Day = day, Source = FootballVoteSource.Official,
                    Value = value, SufferedGoals = suffered, PlayerRole = 0, VoteRole = 0, HasVote = true, IsFinal = true, IsIn = true });
            await database.SaveChangesAsync();
        }
        job = await RunPostponedJobAsync();
        Assert.True(job.Processed >= 1);
        points = await Points(waiting);
        Assert.Equal(5 - 3 + 2, points.Postponed); Assert.Equal(6.5f - 1 + 2, points.Regular); Assert.False(points.Day.AwaitingPostponedMatches);
        var waitingGame = points.Day.Games.Single().Id;
        var actual = (await Game(waiting, waitingGame)).Teams.Single(x => x.TeamId == postponedTeam).Players.Single().Vote!;
        Assert.False(actual.IsPolitical); Assert.Equal(5, actual.Value); Assert.Equal(3, actual.SufferedGoals);

        // A later full recalculation remembers the postponement from the calendar:
        // the default league still uses the political grade despite the actual vote.
        Assert.Equal(new RecalculationOutcome(true, false), await Recalculate(standard));
        Assert.Equal(7 + 2, (await Points(standard)).Postponed);
        Assert.Equal(new RecalculationOutcome(true, false), await Recalculate(waiting));
        Assert.Equal(5 - 3 + 2, (await Points(waiting)).Postponed);
        var footballDay = (await manager.GetFromJsonAsync<FootballCalendarDto>($"/api/football/{year}/calendar"))!.Days.Single(x => x.SerieADay == day);
        var recovered = footballDay.Games.Single(x => x.Ordinal == 1);
        Assert.False(recovered.Delayed); Assert.True(recovered.WasPostponed); Assert.True(recovered.IsFinished); Assert.NotNull(recovered.OriginalDate);
        var votes = (await manager.GetFromJsonAsync<PlayersDto<VotedPlayerDto>>($"/api/football/{year}/days/{day}/votes/official"))!;
        Assert.All(votes.Players, x => Assert.False(x.Vote.IsPolitical)); Assert.Empty(votes.LegacyPoliticalPlayers);
        Assert.Equal(5, votes.Players.Single(x => x.PlayerKey == postponedKeeper).Vote.Value);
        await using var final = factory.Database.CreateContext();
        Assert.Equal(4, await final.FootballVotes.CountAsync(x => x.SeasonIndex == year));
        Assert.Equal(new[] { 44, 44 }, await final.RosterSnapshots.Where(x => x.GroupId == group.GroupId && x.Kind == RosterKind.Matchday)
            .SelectMany(x => x.Players).Select(x => x.Price).ToArrayAsync());
    }

    [PostgreSqlFact]
    public async Task Waiting_leagues_do_not_overwrite_already_saved_days_and_default_leagues_never_join_the_recovery_job()
    {
        const int year = 37; const int day = 4;
        using var manager = await factory.AccountAsync(globalAdmin: true);
        using var participant = await factory.AccountAsync();
        var group = await CreateGroupAsync(manager); var league = await CreateLeagueAsync(manager, group.GroupId, year);
        var owner = (await manager.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var guest = (await participant.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        (await manager.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(guest.Email, "Guest", 2))).EnsureSuccessStatusCode();
        var basket = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!.Baskets[0].Id;
        foreach (var user in new[] { owner, guest })
            (await manager.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year, user.Id == owner.Id ? "First" : "Second", user.Id, []))).EnsureSuccessStatusCode();
        (await manager.PostAsJsonAsync($"/api/competitions/{group.GroupId}/{league}/{year}/calendar/generate", new GenerateCompetitionRequest())).EnsureSuccessStatusCode();
        await using (var database = factory.Database.CreateContext())
        {
            var home = new FootballClub { Name = "Saved fixture home", Abbreviation = "sfh" }; var away = new FootballClub { Name = "Saved fixture away", Abbreviation = "sfa" };
            database.FootballMatchdays.Add(new() { SeasonIndex = year, SerieADay = day, Matches = [new() { SeasonIndex = year, SerieADay = day, Ordinal = 0,
                HomeClub = home, AwayClub = away, OriginalKickoffUtc = DateTimeOffset.UtcNow.AddDays(-2), Delayed = true, WasPostponed = true, ActualOfficialVotesReady = false }] });
            var game = await database.FantasyMatches.SingleAsync(x => x.GroupId == group.GroupId && x.Matchday.SerieADay == day);
            game.Result = new() { HomeGoals = 2, AwayGoals = 1, Home = new() { Value = 70 }, Away = new() { Value = 64 } };
            await database.SaveChangesAsync();
        }
        // Default leagues are never selected by the recovery job: saved values stay.
        await RunPostponedJobAsync();
        await using (var database = factory.Database.CreateContext())
        {
            var untouched = await database.FantasyMatches.Include(x => x.Matchday).SingleAsync(x => x.GroupId == group.GroupId && x.Matchday.SerieADay == day);
            Assert.Equal(70, untouched.Result!.Home!.Value); Assert.False(untouched.Matchday.AwaitingPostponedMatches);
        }
        var graph = (await manager.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        graph.Leagues.Single().Years.Single().Settings.Postponements = new(true);
        (await manager.PutAsJsonAsync($"/api/groups/{group.GroupId}", graph)).EnsureSuccessStatusCode();
        var calendar = (await manager.GetFromJsonAsync<FantasyCalendarDto>($"/api/competitions/{group.GroupId}/{league}/{year}/calendar"))!;
        var refused = await manager.PostAsJsonAsync($"/api/competitions/{group.GroupId}/{league}/{year}/recalculate", new RecalculateCompetitionRequest(day, PreciseDay: true, ExpectedVersion: calendar.Version));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        await using var final = factory.Database.CreateContext();
        var result = (await final.FantasyMatches.SingleAsync(x => x.GroupId == group.GroupId && x.Matchday.SerieADay == day)).Result!;
        Assert.Equal(70, result.Home!.Value); Assert.Equal(2, result.HomeGoals);
    }

    private async Task<SqlJobWorkResult> RunPostponedJobAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ISqlJobActions>().ExecuteAsync("postponed-results", FantacalcioTime.ActualYear, CancellationToken.None);
    }
}
