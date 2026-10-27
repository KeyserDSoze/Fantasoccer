using System.Net.Http.Json;
using Fantasoccer.Application.Core.Football;
using Fantasoccer.Business;
using Fantasoccer.Infrastructure.PostgreSql.Entities;
using Fantasoccer.PostgreSql.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Fantasoccer.RelationalApi.Tests;

public sealed partial class WorkspaceApiTests
{
    [PostgreSqlFact]
    public async Task Minimal_vote_HTTP_and_scoring_preserve_vote_six_with_NULL_historical_club_even_when_annual_club_is_known()
    {
        using var reader = await factory.AccountAsync();
        const string name = "Minimal HTTP keeper"; var key = KeyUtils.GetPlayerKey(name);
        await using (var database = factory.Database.CreateContext())
        {
            var club = await database.Clubs.SingleOrDefaultAsync(x => x.Id == "gen");
            if (club is null) database.Clubs.Add(club = new() { Name = "GENOA" });
            database.PlayerSeasons.Add(new() { Player = new() { Name = name }, Season = new() { SeasonIndex = 71, StartYear = 2082 },
                Club = club, Position = PlayerPosition.Goalkeeper, IsActive = true });
            database.FootballVotes.Add(new() { PlayerId = key, SeasonIndex = 71, Day = 1, Source = FootballVoteSource.Official,
                ClubId = null, Value = 6, HasVote = true, IsActive = false, IsVisible = true });
            await database.SaveChangesAsync();
        }
        var result = (await reader.GetFromJsonAsync<PlayersDto<VotedPlayerDto>>("/api/football/71/days/1/votes/official"))!;
        var voted = Assert.Single(result.Players); Assert.Equal(key, voted.PlayerKey); Assert.Equal(6, voted.Vote.Value);
        Assert.True(voted.Vote.HasVote); Assert.False(voted.IsActive); Assert.True(voted.IsVisible); Assert.Null(voted.RealTeam);
        var catalogue = (await reader.GetFromJsonAsync<PlayersDto<FootballPlayerDto>>("/api/football/71/players"))!;
        Assert.Equal("gen", Assert.Single(catalogue.Players).RealTeam!.ClubCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var input = await scope.ServiceProvider.GetRequiredService<IFootballMatchdayReader>().ReadAsync(71, 1, true, false);
        Assert.NotNull(input.Official); Assert.Null(Assert.Single(input.Official.Players).RealTeam);
        var scoring = scope.ServiceProvider.GetRequiredService<ITeamCalculator>();
        var stats = FootballStatisticsCalculator.Calculate([new() { Name = name, Role = Role.GoalKeeper, RealTeam = new() { Name = "Genoa", Abbreviation = "gen" } }],
            1, new Dictionary<int, VotedRealPlayerWrapper> { [1] = input.Official }, scoring);
        var saved = Assert.Single(stats.Players); Assert.Equal(1, saved.WithVote); Assert.Equal(6, saved.Summatory);
        Assert.Equal(6, Assert.Single(saved.AllGames!).Vote);
    }
}
