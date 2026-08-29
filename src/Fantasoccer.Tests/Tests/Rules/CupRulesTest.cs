using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class CupRulesTest
{
    private readonly IFactory<ILeagueCalculator> _factory;

    public CupRulesTest(IFactory<ILeagueCalculator> factory)
        => _factory = factory;

    [Theory]
    [InlineData(LeagueType.Cup, 4, 12, 2)]
    [InlineData(LeagueType.NewCup, 1, 15, 8)]
    public void GeneratedCalendarsContainEveryTeamOncePerDay(
        LeagueType leagueType,
        int expectedRounds,
        int expectedDays,
        int gamesPerDay)
    {
        var calculator = Calculator(leagueType);
        var settings = LeagueSetting.Default;
        settings.DelayedDay = 2;
        settings.CancelledDay = 1;

        var calendar = calculator.GetRandomCalendar(2026, AnnualLeague(settings), Teams(16));

        Assert.Equal(expectedRounds, calendar.Rounds.Count);
        Assert.All(calendar.Rounds.Values, round =>
        {
            Assert.Equal(expectedDays, round.Count);
            Assert.All(round, day =>
            {
                Assert.Equal(gamesPerDay, day.Games.Count);
                Assert.Equal(gamesPerDay * 2, day.Games
                    .SelectMany(x => new[] { x.HomeOwner, x.AwayOwner })
                    .Distinct()
                    .Count());
            });
            Assert.All(round.Single(x => x.Number == 1).Games, game => Assert.True(game.Result?.IsCancelled));
            Assert.All(round.Where(x => x.Number > 1).SelectMany(x => x.Games), game => Assert.Null(game.Result));
        });
    }

    [Theory]
    [InlineData(LeagueType.Cup, "A")]
    [InlineData(LeagueType.NewCup, "@")]
    public void RankDoesNotMutateExcludedRounds(LeagueType leagueType, string baseRound)
    {
        var calculator = Calculator(leagueType);
        var calendar = new Calendar
        {
            Year = 2026,
            Rounds = new Dictionary<string, List<Day>>
            {
                [baseRound] = [Day(5, 5, Game("T1", "T2", Result(2, 0, 70, 60)), Game("T3", "T4", Result(1, 1, 65, 66)))],
                ["Finals"] = [Day(10, 10, Game("F1", "F2", Result(1, 0)))],
                ["Ignored"] = [Day(11, 11, Game("I1", "I2", Result(1, 0)))]
            }
        };
        if (leagueType == LeagueType.NewCup)
        {
            calendar.Rounds["Europa League"] = [Day(12, 12, Game("E1", "E2", Result(1, 0)))];
            calendar.Rounds["Supercoppa"] = [Day(38, 38, Game("S1", "S2", Result(1, 0)))];
        }
        var excluded = new List<string> { "Ignored" };

        var rank = calculator.GetRank(LeagueSetting.Default, calendar, excluded);

        Assert.Equal(["Ignored"], excluded);
        var teams = Assert.Single(rank.RankedTeams).Value;
        Assert.Equal(["T1", "T4", "T3", "T2"], teams.Select(x => x.Name));
        Assert.Equal(5, rank.SerieADay);
    }

    [Fact]
    public void ClassicCupReturnsNoWinnerBeforeFinal()
    {
        var winners = Calculator(LeagueType.Cup).GetTheYearlyWinners(
            EmptyRank(),
            new Calendar { Year = 2026, Rounds = new Dictionary<string, List<Day>> { ["A"] = [] } });

        Assert.Empty(winners);
    }

    [Fact]
    public void ClassicCupUsesLatestFinalForWinner()
    {
        var calendar = new Calendar
        {
            Year = 2026,
            Rounds = new Dictionary<string, List<Day>>
            {
                ["Finals"] =
                [
                    Day(10, 10, Game("OldHome", "OldAway", Result(1, 0))),
                    Day(20, 20, Game("NewHome", "NewAway", Result(0, 1)))
                ]
            }
        };

        var winner = Assert.Single(Calculator(LeagueType.Cup).GetTheYearlyWinners(EmptyRank(), calendar));

        Assert.Equal("NewAway", winner.Owner);
    }

    [Fact]
    public void CancelledGroupDayAllowsClassicCupProgression()
    {
        var calendar = CancelledCalendar("A", 12, 14);
        var rank = new Rank
        {
            RankedTeams = Enumerable.Range(0, 4).ToDictionary(
                group => ((char)('A' + group)).ToString(),
                group => RankedTeams(group * 4 + 1, 4)),
            SerieADay = 14
        };

        Calculator(LeagueType.Cup).AddDaysInLeague(2026, AnnualLeague(LeagueSetting.Default), [], rank, calendar);

        Assert.Equal(2, calendar.Rounds["Finals"].Count);
        Assert.All(calendar.Rounds["Finals"], day => Assert.Equal(4, day.Games.Count));

        CompleteTwoLegRound(calendar.Rounds["Finals"].TakeLast(2));
        Calculator(LeagueType.Cup).AddDaysInLeague(2026, AnnualLeague(LeagueSetting.Default), [], rank, calendar);
        Assert.Equal([4, 4, 2, 2], calendar.Rounds["Finals"].Select(x => x.Games.Count));

        CompleteTwoLegRound(calendar.Rounds["Finals"].TakeLast(2));
        Calculator(LeagueType.Cup).AddDaysInLeague(2026, AnnualLeague(LeagueSetting.Default), [], rank, calendar);
        Assert.Equal([4, 4, 2, 2, 1], calendar.Rounds["Finals"].Select(x => x.Games.Count));
    }

    [Fact]
    public void CancelledGroupDayAllowsChampionsProgression()
    {
        var calendar = CancelledCalendar("@", 15, 17);
        var rank = new Rank { RankedTeams = new Dictionary<string, List<RankedTeam>> { ["@"] = RankedTeams(1, 16) } };

        Calculator(LeagueType.NewCup).AddDaysInLeague(2026, AnnualLeague(LeagueSetting.Default), [], rank, calendar);

        Assert.Equal(2, calendar.Rounds["Finals"].Count);
        Assert.Equal(2, calendar.Rounds["Europa League"].Count);
        Assert.All(calendar.Rounds["Finals"].Concat(calendar.Rounds["Europa League"]),
            day => Assert.Equal(4, day.Games.Count));
    }

    [Fact]
    public void ChampionsUsesGoalsBeforePointsForSuperCupQualifier()
    {
        var calendar = new Calendar
        {
            Year = 2026,
            Rounds = new Dictionary<string, List<Day>>
            {
                ["@"] = [Day(15, 17, Game("Base1", "Base2", Result(1, 0)))],
                ["Finals"] = [Day(20, 22, Game("ChampHome", "ChampAway", Result(0, 1, 80, 60)))],
                ["Europa League"] = [Day(20, 22, Game("EuropaHome", "EuropaAway", Result(2, 0)))],
            }
        };

        Calculator(LeagueType.NewCup).AddDaysInLeague(
            2026,
            AnnualLeague(LeagueSetting.Default),
            [],
            EmptyRank(),
            calendar);

        var superCup = Assert.Single(Assert.Single(calendar.Rounds["Supercoppa"]).Games);
        Assert.Equal("ChampAway", superCup.HomeOwner);
        Assert.Equal("EuropaHome", superCup.AwayOwner);
    }

    [Fact]
    public void ChampionsUsesLatestFinalsForYearlyWinners()
    {
        var calendar = new Calendar
        {
            Year = 2026,
            Rounds = new Dictionary<string, List<Day>>
            {
                ["Finals"] =
                [
                    Day(10, 10, Game("OldChampHome", "OldChampAway", Result(1, 0))),
                    Day(20, 20, Game("ChampHome", "ChampAway", Result(0, 1)))
                ],
                ["Europa League"] =
                [
                    Day(10, 10, Game("OldEuropaHome", "OldEuropaAway", Result(1, 0))),
                    Day(20, 20, Game("EuropaHome", "EuropaAway", Result(0, 1)))
                ]
            }
        };

        var winners = Calculator(LeagueType.NewCup).GetTheYearlyWinners(EmptyRank(), calendar);

        Assert.Equal(2, winners.Count);
        Assert.Equal("ChampAway", winners.Single(x => x.Round == "Finals").Owner);
        Assert.Equal("EuropaAway", winners.Single(x => x.Round == "Europa League").Owner);
    }

    [Fact]
    public void CompletedClassicCupIsIdempotent()
    {
        var calendar = new Calendar
        {
            Year = 2026,
            Rounds = new Dictionary<string, List<Day>>
            {
                ["A"] = [Day(12, 14, Game("Base1", "Base2", Result(1, 0)))],
                ["Finals"] = [Day(17, 29, Game("Winner", "RunnerUp", Result(2, 1)))]
            }
        };

        Calculator(LeagueType.Cup).AddDaysInLeague(
            2026, AnnualLeague(LeagueSetting.Default), [], EmptyRank(), calendar);

        Assert.Single(calendar.Rounds["Finals"]);
    }

    [Fact]
    public void CompletedChampionsLeagueIsIdempotent()
    {
        var calendar = new Calendar
        {
            Year = 2026,
            Rounds = new Dictionary<string, List<Day>>
            {
                ["@"] = [Day(15, 17, Game("Base1", "Base2", Result(1, 0)))],
                ["Finals"] = [Day(20, 22, Game("Champ1", "Champ2", Result(1, 0)))],
                ["Europa League"] = [Day(20, 22, Game("Europa1", "Europa2", Result(1, 0)))],
                ["Supercoppa"] = [Day(38, 38, Game("Champ1", "Europa1", Result(1, 0)))]
            }
        };

        Calculator(LeagueType.NewCup).AddDaysInLeague(
            2026, AnnualLeague(LeagueSetting.Default), [], EmptyRank(), calendar);

        Assert.Single(calendar.Rounds["Supercoppa"]);
        Assert.Equal(4, calendar.Rounds.Count);
    }

    private ILeagueCalculator Calculator(LeagueType type)
        => _factory.Create(type.ToString())!;

    private static AnnualLeague AnnualLeague(LeagueSetting settings)
        => new() { Year = 2026, Settings = settings };

    private static List<AnnualTeam> Teams(int count)
        => Enumerable.Range(1, count)
            .Select(x => new AnnualTeam { Name = $"Team {x}", Owner = $"owner{x}", AdditionalOwners = [] })
            .ToList();

    private static Calendar CancelledCalendar(string round, int number, int serieADay)
        => new()
        {
            Year = 2026,
            Rounds = new Dictionary<string, List<Day>>
            {
                [round] = [Day(number, serieADay, Game("Team 1", "Team 2", new GameResult { IsCancelled = true }))]
            }
        };

    private static Rank EmptyRank()
        => new() { RankedTeams = [], SerieADay = 0 };

    private static List<RankedTeam> RankedTeams(int start, int count)
        => Enumerable.Range(start, count)
            .Select(x => new RankedTeam { Name = $"Team {x}", Owner = $"owner{x}", ValuePoint = 100 - x })
            .ToList();

    private static void CompleteTwoLegRound(IEnumerable<Day> days)
    {
        var legs = days.ToList();
        Assert.Equal(2, legs.Count);
        foreach (var game in legs[0].Games)
            game.Result = Result(2, 0);
        foreach (var game in legs[1].Games)
            game.Result = Result(0, 1, 65, 70);
    }

    private static Day Day(int number, int serieADay, params CalendarGame[] games)
        => new() { Number = number, SerieADay = serieADay, Games = [.. games] };

    private static CalendarGame Game(string home, string away, GameResult result)
        => new()
        {
            Id = Guid.NewGuid().ToString(),
            Home = home,
            HomeOwner = home,
            Away = away,
            AwayOwner = away,
            Result = result
        };

    private static GameResult Result(
        int homeGoals,
        int awayGoals,
        float homePoints = 70,
        float awayPoints = 65)
        => new()
        {
            Home = new Point(homePoints, false, false, false),
            Away = new Point(awayPoints, false, false, false),
            HomeGoal = homeGoals,
            AwayGoal = awayGoals
        };
}
