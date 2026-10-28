using System.Net;
using System.Net.Http.Json;
using Fantasoccer.Application.Core.Accounts;
using Fantasoccer.Application.Core.Competitions;
using Fantasoccer.Application.Core.Groups;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    private sealed record RankHistoryFixture(HttpClient Client, Guid Group, Guid League, Guid Basket, Guid Home, Guid Away,
        string HomeOwner, string AwayOwner, int Year)
    {
        public string Path => $"/api/competitions/{Group}/{League}/{Year}/rank";
    }

    private async Task<RankHistoryFixture> RankHistoryFixtureAsync(int year)
    {
        var client = await factory.AccountAsync(globalAdmin: true);
        using var guest = await factory.AccountAsync();
        var group = await CreateGroupAsync(client); var league = await CreateLeagueAsync(client, group.GroupId, year);
        var owner = (await client.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        var other = (await guest.GetFromJsonAsync<AccountIdentityDto>("/api/accounts/me"))!;
        (await client.PutAsJsonAsync($"/api/workspace/groups/{group.GroupId}/members", new SetGroupMemberRequest(other.Email, "Guest", 2))).EnsureSuccessStatusCode();
        var graph = (await client.GetFromJsonAsync<GameGroupDto>($"/api/groups/{group.GroupId}"))!;
        var basket = graph.Baskets[0].Id;
        foreach (var account in new[] { owner, other })
            (await client.PostAsJsonAsync($"/api/workspace/groups/{group.GroupId}/teams", new CreateTeamRequest(basket, year,
                account.Id == owner.Id ? "Current home" : "Current away", account.Id, []))).EnsureSuccessStatusCode();
        await using var database = factory.Database.CreateContext();
        var teams = await database.RosterSnapshots.Where(x => x.GroupId == group.GroupId && x.Kind == RosterKind.Season).ToArrayAsync();
        return new(client, group.GroupId, league, basket, teams.Single(x => x.OwnerUserId == owner.Id).TeamId,
            teams.Single(x => x.OwnerUserId == other.Id).TeamId, owner.Email, other.Email, year);
    }

    private static SavedFantasyResult HistoryResult(int home, int away, float homePoints = 74, float awayPoints = 67) => new()
    { HomeGoals = home, AwayGoals = away, Home = new() { Value = homePoints }, Away = new() { Value = awayPoints } };

    private static FantasyStage HistoryStage(RankHistoryFixture fixture, string stage, int ordinal,
        params (int Day, SavedFantasyResult? Result)[] results) => new()
    {
        GroupId = fixture.Group, LeagueId = fixture.League, SeasonIndex = fixture.Year, Stage = stage, Ordinal = ordinal,
        Days = results.Select((value, index) => new FantasyMatchday
        {
            GroupId = fixture.Group, LeagueId = fixture.League, SeasonIndex = fixture.Year, Stage = stage, Ordinal = index,
            SerieADay = value.Day, FantasyTurn = index + 10, Games = [new()
            {
                GroupId = fixture.Group, LeagueId = fixture.League, SeasonIndex = fixture.Year, Id = Guid.NewGuid(), Stage = stage,
                DayOrdinal = index, Ordinal = 0, Number = 1, HomeTeamId = fixture.Home, HomeBasketId = fixture.Basket,
                AwayTeamId = fixture.Away, AwayBasketId = fixture.Basket, HomeName = "Saved home", AwayName = "Saved away",
                HomeOwner = index == 0 ? fixture.HomeOwner.ToUpperInvariant() : fixture.HomeOwner, AwayOwner = fixture.AwayOwner, Result = value.Result
            }]
        }).ToArray()
    };

    [PostgreSqlFact]
    public async Task Historical_rank_filters_saved_results_by_Serie_A_day_without_writing_snapshots_or_recalculating_votes_or_balances()
    {
        var fixture = await RankHistoryFixtureAsync(32); using var client = fixture.Client;
        using var outsider = await factory.AccountAsync();
        await using (var database = factory.Database.CreateContext())
        {
            var rules = await database.LeagueRules.SingleAsync(x => x.GroupId == fixture.Group && x.LeagueId == fixture.League && x.SeasonIndex == fixture.Year);
            rules.PointForVictory = 5; rules.PointForDraw = 2; rules.PointForDefeat = -1; rules.MoneyForGoal = 3; rules.MoneyForSufferedGoal = 1;
            database.FantasyCalendars.Add(new() { GroupId = fixture.Group, LeagueId = fixture.League, SeasonIndex = fixture.Year, Version = 9, Stages =
            [HistoryStage(fixture, "@", 0, (3, HistoryResult(3, 1, 80.5f)), (7, HistoryResult(2, 2, 71, 70)), (8, null),
                (9, new() { IsCancelled = true }), (12, HistoryResult(0, 4))), HistoryStage(fixture, "Future", 1, (11, HistoryResult(9, 0))),
                HistoryStage(fixture, "Empty", 2)] });
            database.FantasyStandingSnapshots.Add(new() { GroupId = fixture.Group, LeagueId = fixture.League, SeasonIndex = fixture.Year, SnapshotDay = 0, SerieADay = 12,
                Stages = [new() { Stage = "@", Teams = [new() { TeamId = fixture.Home, BasketId = fixture.Basket, Name = "Original rank name",
                    Owner = fixture.HomeOwner, Points = 777, Money = 123, PlusMoney = 8 }] }] });
            var roster = await database.RosterSnapshots.SingleAsync(x => x.GroupId == fixture.Group && x.TeamId == fixture.Home && x.Kind == RosterKind.Season);
            roster.MoneyFromRank = 54; roster.RankCreditGranted = 19;
            await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync(fixture.Path + "?day=7")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(fixture.Path + "?day=39")).StatusCode);
        var before = (await client.GetFromJsonAsync<FantasyRankDto>(fixture.Path + "?day=2"))!;
        Assert.True(before.IsReconstructed); Assert.Equal(0, before.SerieADay); Assert.Empty(before.Rounds);
        foreach (var day in new[] { 7, 8 })
        {
            var rank = (await client.GetFromJsonAsync<FantasyRankDto>(fixture.Path + "?day=" + day))!;
            Assert.True(rank.IsReconstructed); Assert.Equal(day, rank.SnapshotDay); Assert.Equal(7, rank.SerieADay);
            Assert.Equal(new[] { "@" }, rank.Rounds.Keys);
            var home = rank.Rounds["@"].Single(x => x.TeamId == fixture.Home); var away = rank.Rounds["@"].Single(x => x.TeamId == fixture.Away);
            Assert.Equal("Saved home", home.Name); Assert.Equal(7, home.Point); Assert.Equal(1, away.Point);
            Assert.Equal(1, home.Victories); Assert.Equal(1, home.Draws); Assert.Equal(5, home.Goal); Assert.Equal(3, home.SufferedGoal);
            Assert.Equal(151.5f, home.ValuePoint); Assert.Equal(18, home.PlusMoney);
            Assert.Null(home.Money); Assert.Null(home.ValueAssets);
        }
        var cancelled = (await client.GetFromJsonAsync<FantasyRankDto>(fixture.Path + "?day=9"))!;
        Assert.Equal(9, cancelled.SerieADay); Assert.Equal(7, cancelled.Rounds["@"][0].Point);
        var current = (await client.GetFromJsonAsync<FantasyRankDto>(fixture.Path))!;
        Assert.False(current.IsReconstructed); Assert.Equal(0, current.SnapshotDay); Assert.Equal(777, current.Rounds["@"][0].Point);
        Assert.Equal(123, current.Rounds["@"][0].Money); Assert.Equal(131, current.Rounds["@"][0].ValueAssets);
        await using var final = factory.Database.CreateContext();
        Assert.Equal(1, await final.FantasyStandingSnapshots.CountAsync(x => x.GroupId == fixture.Group));
        Assert.False(await final.FantasyStandingSnapshots.AnyAsync(x => x.GroupId == fixture.Group && x.SnapshotDay != 0));
        Assert.Equal(9, (await final.FantasyCalendars.SingleAsync(x => x.GroupId == fixture.Group)).Version);
        Assert.False(await final.PlayedMatchSnapshots.AnyAsync(x => x.GroupId == fixture.Group));
        Assert.False(await final.FootballVotes.AnyAsync(x => x.SeasonIndex == fixture.Year));
        var saved = await final.RosterSnapshots.SingleAsync(x => x.GroupId == fixture.Group && x.TeamId == fixture.Home && x.Kind == RosterKind.Season);
        Assert.Equal(54, saved.MoneyFromRank); Assert.Equal(19, saved.RankCreditGranted);
    }

    [PostgreSqlFact]
    public async Task Historical_rank_uses_annual_rules_and_competition_type_and_reflects_corrections_without_replacing_current_rank()
    {
        var fixture = await RankHistoryFixtureAsync(33); using var client = fixture.Client;
        await using (var database = factory.Database.CreateContext())
        {
            database.FantasyCalendars.Add(new() { GroupId = fixture.Group, LeagueId = fixture.League, SeasonIndex = fixture.Year, Stages =
                new[] { "@", "Finals", "Europa League", "Supercoppa" }.Select((stage, index) => HistoryStage(fixture, stage, index, (3, HistoryResult(2, 1)))).ToArray() });
            await database.SaveChangesAsync();
        }
        foreach (var type in new[] { LeagueType.League, LeagueType.Cup, LeagueType.NewCup, LeagueType.SuperLeague })
        {
            await using (var database = factory.Database.CreateContext())
            {
                var annual = await database.LeagueSeasons.Include(x => x.League).SingleAsync(x => x.GroupId == fixture.Group && x.LeagueId == fixture.League && x.SeasonIndex == fixture.Year);
                annual.CompetitionType = (int)type; annual.League.CompetitionType = (int)LeagueType.NewCup;
                var rules = await database.LeagueRules.SingleAsync(x => x.GroupId == fixture.Group && x.LeagueId == fixture.League && x.SeasonIndex == fixture.Year);
                rules.PointForVictory = 6; rules.RankWithValuePoints = type == LeagueType.SuperLeague;
                await database.SaveChangesAsync();
            }
            var rank = (await client.GetFromJsonAsync<FantasyRankDto>(fixture.Path + "?day=3"))!;
            var excluded = type == LeagueType.NewCup ? new[] { "Finals", "Europa League", "Supercoppa" } : type == LeagueType.Cup ? new[] { "Finals" } : [];
            Assert.Equal(4 - excluded.Length, rank.Rounds.Count);
            foreach (var stage in excluded) Assert.False(rank.Rounds.ContainsKey(stage));
            Assert.Equal(type == LeagueType.SuperLeague ? 80 : 6, rank.Rounds["@"].Single(x => x.TeamId == fixture.Home).Point);
        }
        await using (var database = factory.Database.CreateContext())
        {
            var rules = await database.LeagueRules.SingleAsync(x => x.GroupId == fixture.Group && x.LeagueId == fixture.League && x.SeasonIndex == fixture.Year);
            rules.RankWithValuePoints = false; rules.PointForVictory = 9;
            var game = await database.FantasyMatches.SingleAsync(x => x.GroupId == fixture.Group && x.Stage == "@");
            game.Result!.HomeGoals = 0; game.Result.AwayGoals = 3;
            await database.SaveChangesAsync();
        }
        var corrected = (await client.GetFromJsonAsync<FantasyRankDto>(fixture.Path + "?day=3"))!;
        Assert.Equal(9, corrected.Rounds["@"].Single(x => x.TeamId == fixture.Away).Point);
        await using var final = factory.Database.CreateContext();
        Assert.False(await final.FantasyStandingSnapshots.AnyAsync(x => x.GroupId == fixture.Group));
    }

    [PostgreSqlFact]
    public async Task Historical_rank_tracks_annual_team_IDs_for_late_participants_without_merging_equal_owner_labels_or_leaking_other_scopes()
    {
        var fixture = await RankHistoryFixtureAsync(35); using var client = fixture.Client;
        var third = Guid.NewGuid();
        await using (var database = factory.Database.CreateContext())
        {
            // A historical calendar identity is authoritative even when its
            // label is shared by another annual team. Do not join by that label.
            database.TeamSeasons.Add(new() { Id = third, GroupId = fixture.Group, BasketId = fixture.Basket, SeasonIndex = fixture.Year,
                Name = "Historical third", NormalizedName = "historical third" });
            // Matches only involve explicitly admitted participants.
            database.LeagueParticipations.Add(new() { GroupId = fixture.Group, LeagueId = fixture.League, SeasonIndex = fixture.Year, TeamId = third,
                BasketId = fixture.Basket, Source = ParticipationSource.Manual, AdmittedAtUtc = DateTimeOffset.UtcNow });
            var stage = HistoryStage(fixture, "@", 0, (3, HistoryResult(2, 1)), (7, HistoryResult(0, 3)));
            var later = stage.Days.Last().Games.Single();
            later.HomeTeamId = third; later.HomeName = "Saved third"; later.HomeOwner = fixture.HomeOwner;
            database.FantasyCalendars.Add(new() { GroupId = fixture.Group, LeagueId = fixture.League, SeasonIndex = fixture.Year, Stages = [stage] });
            await database.SaveChangesAsync();
        }
        var early = (await client.GetFromJsonAsync<FantasyRankDto>(fixture.Path + "?day=3"))!;
        Assert.Equal(2, early.Rounds["@"].Length); Assert.DoesNotContain(early.Rounds["@"], x => x.TeamId == third);
        var late = (await client.GetFromJsonAsync<FantasyRankDto>(fixture.Path + "?day=7"))!;
        Assert.Equal(3, late.Rounds["@"].Length);
        Assert.Equal(1, late.Rounds["@"].Single(x => x.TeamId == fixture.Home).Victories);
        Assert.Equal(1, late.Rounds["@"].Single(x => x.TeamId == third).Defeats);
        Assert.Equal("Saved third", late.Rounds["@"].Single(x => x.TeamId == third).Name);
        var otherResponse = await client.PostAsJsonAsync($"/api/workspace/groups/{fixture.Group}/leagues", new CreateLeagueRequest("Other league", fixture.Year, [fixture.Basket]));
        otherResponse.EnsureSuccessStatusCode();
        var otherLeague = (await otherResponse.Content.ReadFromJsonAsync<LeagueCreatedDto>())!.LeagueId;
        Assert.Equal(HttpStatusCode.NoContent, (await client.GetAsync($"/api/competitions/{fixture.Group}/{otherLeague}/{fixture.Year}/rank?day=7")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/competitions/{fixture.Group}/{fixture.League}/{fixture.Year + 1}/rank?day=7")).StatusCode);
        await using var final = factory.Database.CreateContext();
        Assert.False(await final.FantasyStandingSnapshots.AnyAsync(x => x.GroupId == fixture.Group));
    }

    [PostgreSqlFact]
    public async Task Historical_rank_does_not_invent_missing_identities_or_incomplete_scores_and_unassigned_brackets_remain_empty()
    {
        var fixture = await RankHistoryFixtureAsync(34); using var client = fixture.Client;
        await using (var database = factory.Database.CreateContext())
        {
            var stage = HistoryStage(fixture, "@", 0, (3, null)); var game = stage.Days.Single().Games.Single();
            game.HomeTeamId = null; game.HomeBasketId = null; game.AwayTeamId = null; game.AwayBasketId = null;
            database.FantasyCalendars.Add(new() { GroupId = fixture.Group, LeagueId = fixture.League, SeasonIndex = fixture.Year, Stages = [stage] });
            await database.SaveChangesAsync();
        }
        var empty = (await client.GetFromJsonAsync<FantasyRankDto>(fixture.Path + "?day=3"))!;
        Assert.Empty(empty.Rounds);
        await using (var database = factory.Database.CreateContext())
        {
            var game = await database.FantasyMatches.SingleAsync(x => x.GroupId == fixture.Group); game.Result = HistoryResult(2, 1);
            await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(fixture.Path + "?day=3")).StatusCode);
        await using (var database = factory.Database.CreateContext())
        {
            var game = await database.FantasyMatches.SingleAsync(x => x.GroupId == fixture.Group);
            game.HomeTeamId = fixture.Home; game.HomeBasketId = fixture.Basket; game.AwayTeamId = fixture.Away; game.AwayBasketId = fixture.Basket;
            game.Result!.Away = null; await database.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync(fixture.Path + "?day=3")).StatusCode);
        await using var final = factory.Database.CreateContext();
        Assert.False(await final.FantasyStandingSnapshots.AnyAsync(x => x.GroupId == fixture.Group));
    }
}
