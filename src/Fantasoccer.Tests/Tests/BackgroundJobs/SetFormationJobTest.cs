using Fantasoccer.Engine.IngestionData;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.BackgroundJobs;

[Collection("InMemoryRepository")]
public sealed class SetFormationJobTest : IAsyncLifetime
{
    private const string GroupId = "set-formation-job-test";
    private const string BasketId = "set-formation-basket-test";
    private const string Owner = "formation@test.local";
    private readonly SetFormationJob _job;
    private readonly IRepository<Team, TeamDayKey> _teams;
    private readonly IRepository<RealCalendar, int> _calendars;
    private readonly IRepository<Group, string> _groups;
    private readonly List<Group> _groupSnapshot = [];
    private RealCalendar? _calendarSnapshot;

    public SetFormationJobTest(
        IFantasoccerLogger logger,
        IRepository<Team, TeamDayKey> teams,
        IRepository<RealCalendar, int> calendars,
        IRepository<Group, string> groups)
    {
        _teams = teams;
        _calendars = calendars;
        _groups = groups;
        _job = new SetFormationJob(logger, teams, calendars, groups);
    }

    public async ValueTask InitializeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        _calendarSnapshot = await _calendars.GetAsync(FantacalcioTime.ActualYear, cancellationToken);
        await _calendars.DeleteAsync(FantacalcioTime.ActualYear, cancellationToken);
        _groupSnapshot.AddRange(await _groups.ToListAsEntityAsync(cancellationToken: cancellationToken));
        foreach (var group in _groupSnapshot)
            await _groups.DeleteAsync(group.Id, cancellationToken);
        await CleanupTeamsAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await _groups.DeleteAsync(GroupId, cancellationToken);
        foreach (var group in _groupSnapshot)
            await _groups.InsertAsync(group.Id, group, cancellationToken);
        await CleanupTeamsAsync(cancellationToken);
        await _calendars.DeleteAsync(FantacalcioTime.ActualYear, cancellationToken);
        if (_calendarSnapshot != null)
            await _calendars.InsertAsync(FantacalcioTime.ActualYear, _calendarSnapshot, cancellationToken);
    }

    [Fact]
    public async Task DoesNothingWhenCalendarIsMissing()
    {
        await InsertGroupAsync();
        await _teams.InsertAsync(Key(7), Team("Current formation"), TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Null(await _teams.GetAsync(Key(8), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DoesNotCreateDayThirtyNineAtEndOfSeason()
    {
        await InsertCompletedCalendarAsync(38);
        await InsertGroupAsync();
        await _teams.InsertAsync(Key(38), Team("Last formation"), TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Null(await _teams.GetAsync(Key(39), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CopiesCurrentFormationToNextDay()
    {
        await InsertCompletedCalendarAsync(7);
        await InsertGroupAsync();
        var currentFormation = Team("Current formation");
        currentFormation.FormationChanges = 2;
        currentFormation.FormationChangeHistory =
        [
            new LiveFormationChange { Author = Owner, Date = FantacalcioTime.ItalyNow }
        ];
        await _teams.InsertAsync(Key(7), currentFormation, TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var nextFormation = await _teams.GetAsync(Key(8), TestContext.Current.CancellationToken);
        Assert.NotNull(nextFormation);
        Assert.Equal("Current formation", nextFormation.Name);
        Assert.Equal("Player", Assert.Single(nextFormation.Players).Name);
        Assert.Equal(0, nextFormation.FormationChanges);
        Assert.Empty(nextFormation.FormationChangeHistory);
        var sourceFormation = await _teams.GetAsync(Key(7), TestContext.Current.CancellationToken);
        Assert.Equal(2, sourceFormation?.FormationChanges);
        Assert.Single(sourceFormation?.FormationChangeHistory ?? []);
    }

    [Fact]
    public async Task DoesNotOverwriteExistingNextFormation()
    {
        await InsertCompletedCalendarAsync(7);
        await InsertGroupAsync();
        await _teams.InsertAsync(Key(7), Team("Current formation"), TestContext.Current.CancellationToken);
        var existingFormation = Team("Existing next formation");
        existingFormation.FormationChanges = 2;
        existingFormation.FormationChangeHistory = [new LiveFormationChange { Author = Owner }];
        await _teams.InsertAsync(Key(8), existingFormation, TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var nextFormation = await _teams.GetAsync(Key(8), TestContext.Current.CancellationToken);
        Assert.NotNull(nextFormation);
        Assert.Equal("Existing next formation", nextFormation.Name);
        Assert.Equal(0, nextFormation.FormationChanges);
        Assert.Empty(nextFormation.FormationChangeHistory);
    }

    [Fact]
    public async Task DoesNotCreateFormationWhenSourceIsMissing()
    {
        await InsertCompletedCalendarAsync(7);
        await InsertGroupAsync();

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Null(await _teams.GetAsync(Key(8), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task IgnoresBasketWithMissingTeams()
    {
        await InsertCompletedCalendarAsync(7);
        await _groups.InsertAsync(GroupId, new Group
        {
            Id = GroupId,
            Name = "Formation group",
            Users = [],
            Leagues = [],
            Baskets =
            [
                new Basket
                {
                    Id = BasketId,
                    Name = "Formation basket",
                    Years =
                    [
                        new YearlyBasket
                        {
                            Year = FantacalcioTime.ActualYear,
                            Teams = null!
                        }
                    ]
                }
            ]
        }, TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        Assert.Null(await _teams.GetAsync(Key(8), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PrefersLiveFormationOverLastCompletedDay()
    {
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    SerieADay = 7,
                    Games = [new RealGame { Date = FantacalcioTime.ItalyNow.AddHours(-12), Delayed = false }]
                },
                new RealDay
                {
                    SerieADay = 8,
                    Games = [new RealGame { Date = FantacalcioTime.ItalyNow.AddMinutes(-10), Delayed = false }]
                }
            ]
        }, TestContext.Current.CancellationToken);
        await InsertGroupAsync();
        await _teams.InsertAsync(Key(7), Team("Completed formation"), TestContext.Current.CancellationToken);
        await _teams.InsertAsync(Key(8), Team("Live formation"), TestContext.Current.CancellationToken);

        await _job.ActionToDoAsync(TestContext.Current.CancellationToken);

        var nextFormation = await _teams.GetAsync(Key(9), TestContext.Current.CancellationToken);
        Assert.NotNull(nextFormation);
        Assert.Equal("Live formation", nextFormation.Name);
    }

    private async Task InsertCompletedCalendarAsync(int day)
    {
        await _calendars.InsertAsync(FantacalcioTime.ActualYear, new RealCalendar
        {
            Year = FantacalcioTime.ActualYear,
            Days =
            [
                new RealDay
                {
                    Year = FantacalcioTime.ActualYear,
                    SerieADay = day,
                    Games =
                    [
                        new RealGame
                        {
                            Home = new RealTeam { Name = "Roma" },
                            Away = new RealTeam { Name = "Milan" },
                            Date = FantacalcioTime.ItalyNow.AddHours(-12)
                        }
                    ]
                }
            ]
        }, TestContext.Current.CancellationToken);
    }

    private async Task InsertGroupAsync()
    {
        await _groups.InsertAsync(GroupId, new Group
        {
            Id = GroupId,
            Name = "Formation group",
            Users = [],
            Leagues = [],
            Baskets =
            [
                new Basket
                {
                    Id = BasketId,
                    Name = "Formation basket",
                    Years =
                    [
                        new YearlyBasket
                        {
                            Year = FantacalcioTime.ActualYear,
                            Teams = [new AnnualTeam { Name = "Formation", Owner = Owner, AdditionalOwners = [] }]
                        }
                    ]
                }
            ]
        }, TestContext.Current.CancellationToken);
    }

    private static Team Team(string name)
        => new()
        {
            Name = name,
            Owner = Owner,
            AdditionalOwners = [],
            Players =
            [
                new Player
                {
                    Name = "Player",
                    RealTeam = new RealTeam { Name = "Roma" },
                    Role = Role.Forward,
                    Status = PlayerInTeamStatus.Active
                }
            ]
        };

    private static TeamDayKey Key(int day)
        => new()
        {
            Year = FantacalcioTime.ActualYear,
            Group = GroupId,
            BasketId = BasketId,
            Day = day,
            Email = Owner
        };

    private async Task CleanupTeamsAsync(CancellationToken cancellationToken)
    {
        foreach (var day in new[] { 7, 8, 9, 38, 39 })
            await _teams.DeleteAsync(Key(day), cancellationToken);
    }
}
