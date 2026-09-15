using Fantasoccer.Business;
using Fantasoccer.Business.BackgroundJobs.GroupsManager;
using Fantasoccer.InMemory;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using RepositoryFramework.InMemory;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class HallOfFameJobTest : IAsyncLifetime
{
    private const string GroupId = "hall-of-fame-job-test";
    private const string LeagueId = "hall-of-fame-league-test";
    private const string BasketId = "hall-of-fame-basket-test";
    private const string Winner = "hall-winner@test.local";
    private const string Loser = "hall-loser@test.local";
    private readonly int _year = FantacalcioTime.ActualYear - 1;
    private readonly ServiceProvider _services;
    private readonly IRepository<Group, string> _groups;
    private readonly IRepository<Calendar, LeagueKey> _calendars;
    private readonly IRepository<Rank, LeagueKey> _ranks;
    private readonly IRepository<HallOfFame, GeneralLeagueKey> _hallOfFame;
    private readonly IRepository<Team, TeamKey> _teams;
    private readonly List<Group> _groupSnapshot = [];

    public HallOfFameJobTest()
    {
        var services = new ServiceCollection();
        services.AddInMemoryInfrastructures();
        services.AddRepository<HallOfFame, GeneralLeagueKey>(builder => builder.WithInMemory());
        services.AddSharedBusiness();
        _services = services.BuildServiceProvider();
        _groups = _services.GetRequiredService<IRepository<Group, string>>();
        _calendars = _services.GetRequiredService<IRepository<Calendar, LeagueKey>>();
        _ranks = _services.GetRequiredService<IRepository<Rank, LeagueKey>>();
        _hallOfFame = _services.GetRequiredService<IRepository<HallOfFame, GeneralLeagueKey>>();
        _teams = _services.GetRequiredService<IRepository<Team, TeamKey>>();
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _groupSnapshot.AddRange(await _groups.ToListAsEntityAsync(cancellationToken: cancellationToken));
        foreach (var group in _groupSnapshot)
            await _groups.DeleteAsync(group.Id, cancellationToken);
        await CleanupAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _groups.DeleteAsync(GroupId, cancellationToken);
        await CleanupAsync(cancellationToken);
        foreach (var group in _groupSnapshot)
            await _groups.InsertAsync(group.Id, group, cancellationToken);
    }

    [Fact]
    public async Task AggregatesRankRecordGameTeamAndPlayers()
    {
        await _groups.InsertAsync(GroupId, Group(), TestContext.Current.CancellationToken);
        await _ranks.InsertAsync(LeagueKey(), Rank(), TestContext.Current.CancellationToken);
        await _calendars.InsertAsync(LeagueKey(), Calendar(), TestContext.Current.CancellationToken);
        await _teams.InsertAsync(TeamKey(), Team(), TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var result = await _hallOfFame.GetAsync(HallOfFameKey(), TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Equal(2, result.Ranks.Count);
        Assert.Equal(10, result.Ranks.Single(x => x.Owner == Winner).Point);
        Assert.Equal(_year, result.RecordGame.Year);
        Assert.Equal(72, result.RecordGame.Game.Result!.Home.Value);
        var winningTeam = Assert.Single(result.WinningTeams);
        Assert.Equal(Winner, winningTeam.Owner);
        Assert.Equal([_year], winningTeam.Wins["@"][..]);
        var winningPlayer = Assert.Single(result.WinningPlayers);
        Assert.Equal("Winning player", winningPlayer.Player.Name);
        Assert.Equal([_year], winningPlayer.Wins["@"][..]);
    }

    [Fact]
    public async Task SkipsLeagueWhenRankOrCalendarIsMissing()
    {
        await _groups.InsertAsync(GroupId, Group(), TestContext.Current.CancellationToken);

        await CreateJob().ActionToDoAsync(TestContext.Current.CancellationToken);

        var result = await _hallOfFame.GetAsync(HallOfFameKey(), TestContext.Current.CancellationToken);
        Assert.NotNull(result);
        Assert.Empty(result.Ranks);
        Assert.Empty(result.WinningTeams);
    }

    private HallOfFameJob CreateJob()
        => new(
            _services.GetRequiredService<IFantasoccerLogger>(),
            _groups,
            _calendars,
            _ranks,
            _hallOfFame,
            _teams,
            _services.GetRequiredService<IFactory<ILeagueCalculator>>(),
            _services.GetRequiredService<IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>>>());

    private Group Group()
        => new()
        {
            Id = GroupId,
            Name = "Hall of fame test",
            Users = [],
            Leagues =
            [
                new League
                {
                    Id = LeagueId,
                    Name = "League",
                    Type = LeagueType.League,
                    BasketsId = [BasketId],
                    Years = [new AnnualLeague { Year = _year, Settings = LeagueSetting.Default }]
                }
            ],
            Baskets =
            [
                new Basket
                {
                    Id = BasketId,
                    Name = "Basket",
                    Years =
                    [
                        new YearlyBasket
                        {
                            Year = _year,
                            Teams =
                            [
                                new AnnualTeam { Name = "Winner", Owner = Winner, AdditionalOwners = [] },
                                new AnnualTeam { Name = "Loser", Owner = Loser, AdditionalOwners = [] }
                            ]
                        }
                    ]
                }
            ]
        };

    private Rank Rank()
        => new()
        {
            SerieADay = 38,
            RankedTeams = new Dictionary<string, List<RankedTeam>>
            {
                ["@"] =
                [
                    new RankedTeam { Name = "Winner", Owner = Winner, Point = 10, Victories = 3, Goal = 5 },
                    new RankedTeam { Name = "Loser", Owner = Loser, Point = 5, Victories = 1, Goal = 2 }
                ]
            }
        };

    private Calendar Calendar()
        => new()
        {
            Year = _year,
            Rounds = new Dictionary<string, List<Day>>
            {
                ["@"] =
                [
                    new Day
                    {
                        Number = 1,
                        SerieADay = 1,
                        Games =
                        [
                            new CalendarGame
                            {
                                Id = "hall-game",
                                Home = "Winner",
                                HomeOwner = Winner,
                                Away = "Loser",
                                AwayOwner = Loser,
                                Result = new GameResult
                                {
                                    Home = new Point(72, false, false, false),
                                    Away = new Point(65, false, false, false),
                                    HomeGoal = 2,
                                    AwayGoal = 0
                                }
                            }
                        ]
                    }
                ]
            }
        };

    private Team Team()
        => new()
        {
            Name = "Winner",
            Owner = Winner,
            AdditionalOwners = [],
            Players =
            [
                new Player
                {
                    Name = "Winning player",
                    Role = Role.Forward,
                    Status = PlayerInTeamStatus.Active
                }
            ]
        };

    private LeagueKey LeagueKey()
        => new() { Group = GroupId, League = LeagueId, Year = _year };

    private GeneralLeagueKey HallOfFameKey()
        => new() { Group = GroupId, League = LeagueId };

    private TeamKey TeamKey()
        => new() { Group = GroupId, BasketId = BasketId, Email = Winner, Year = _year };

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        await _calendars.DeleteAsync(LeagueKey(), cancellationToken);
        await _ranks.DeleteAsync(LeagueKey(), cancellationToken);
        await _hallOfFame.DeleteAsync(HallOfFameKey(), cancellationToken);
        await _teams.DeleteAsync(TeamKey(), cancellationToken);
    }
}
