using Microsoft.Extensions.DependencyInjection;
using Fantasoccer.Business;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class TeamCalculatorRulesTest
{
    private readonly ITeamCalculator _calculator;

    public TeamCalculatorRulesTest(ITeamCalculator calculator)
        => _calculator = calculator;

    [Fact]
    public void AppliesConfiguredVoteBonusesAndMaluses()
    {
        var settings = LeagueSetting.Default;
        settings.Votes[Role.Forward] = new VoteLeagueSetting
        {
            Goal = 4,
            Assist = 2,
            OwnGoal = -5,
            YellowCard = -1
        };
        _calculator.SetLeagueSettings(LeagueType.League, settings);

        var player = Player(Role.Forward);
        var value = _calculator.ForceEnrich(player, new Vote
        {
            Value = 6,
            Goal = 1,
            Assist = 1,
            OwnGoal = 1,
            Status = Behaviour.YellowCard,
            HasVote = true
        }, null, null);

        Assert.Equal(6, value.FinalValue.Value);
    }

    [Fact]
    public void AppliesCleanSheetOnlyToGoalkeeperWithoutSufferedGoals()
    {
        var settings = LeagueSetting.Default;
        settings.PointForCleanSheet = 1;
        _calculator.SetLeagueSettings(LeagueType.League, settings);

        var goalkeeper = _calculator.ForceEnrich(Player(Role.GoalKeeper), new Vote
        {
            Value = 6,
            HasVote = true
        }, null, null);
        var conceded = _calculator.ForceEnrich(Player(Role.GoalKeeper), new Vote
        {
            Value = 6,
            SufferedGoal = 1,
            HasVote = true
        }, null, null);

        Assert.Equal(7, goalkeeper.FinalValue.Value);
        Assert.Equal(5, conceded.FinalValue.Value);
        Assert.True(goalkeeper.FinalValue.Special);
        Assert.False(conceded.FinalValue.Special);
    }

    [Theory]
    [InlineData(3, 23)]
    [InlineData(4, 30)]
    [InlineData(5, 37)]
    public void CalculatesGoodPeopleAndDefensiveBonuses(int defenders, int expected)
    {
        var settings = LeagueSetting.Default;
        settings.PointForGoodPeople = 2;
        settings.PointForStrongDefense = 3;
        settings.PointForStrongDefense4 = 4;
        settings.PointForStrongDefense5 = 5;
        _calculator.SetLeagueSettings(LeagueType.League, settings);

        var players = Enumerable.Range(0, defenders)
            .Select(_ => _calculator.ForceEnrich(Player(Role.Defensor), new Vote
            {
                Value = 6,
                Status = Behaviour.Nothing,
                HasVote = true
            }, null, null));

        var point = _calculator.CalculatePoint(players);

        Assert.Equal(expected, point.Value);
        Assert.True(point.DefensiveBonus);
        Assert.True(point.GoodPeople);
    }

    [Fact]
    public void AppliesEveryConfiguredVoteEvent()
    {
        var settings = LeagueSetting.Default;
        settings.Votes[Role.Undefined] = new VoteLeagueSetting
        {
            Goal = 2,
            Penalty = 3,
            SufferedGoal = -1,
            StoppedPenalty = 4,
            WrongedPenalty = -3,
            OwnGoal = -2,
            Assist = 1.5f,
            RedCard = -1.5f,
            Injury = -1,
            ManOfTheMatch = 2
        };
        _calculator.SetLeagueSettings(LeagueType.League, settings);

        var value = _calculator.ForceEnrich(Player(Role.Midfielder), new Vote
        {
            Value = 6,
            Goal = 2,
            Penalty = 1,
            SufferedGoal = 2,
            StoppedPenalty = 1,
            WrongedPenalty = 1,
            OwnGoal = 1,
            Assist = 2,
            Status = Behaviour.RedCard,
            Injured = true,
            ManOfTheMatch = true,
            HasVote = true
        }, null, null).FinalValue;

        Assert.Equal(12.5f, value.Value);
    }

    [Fact]
    public void MarksOwnGoalWhenScoreIsBelowConfiguredThreshold()
    {
        var settings = LeagueSetting.Default;
        settings.PointForFirstGoal = 66;
        settings.PointForOwnGoal = 6;
        settings.PointForGoodPeople = 0;
        _calculator.SetLeagueSettings(LeagueType.League, settings);

        var point = _calculator.CalculatePoint([
            _calculator.ForceEnrich(Player(Role.Forward), new Vote { Value = 59, HasVote = true }, null, null)
        ]);

        Assert.True(point.OwnGoal);
    }

    [Fact]
    public void BestFormationSelectsHighestScoringValidElevenFromFullRoster()
    {
        var settings = LeagueSetting.Default;
        settings.Formation = FormationType.Best;
        settings.PointForGoodPeople = 0;
        settings.PointForStrongDefense = 0;
        settings.PointForStrongDefense4 = 0;
        settings.PointForStrongDefense5 = 0;
        settings.PointForCleanSheet = 0;
        _calculator.SetLeagueSettings(LeagueType.League, settings);
        var roster = Roster(Role.GoalKeeper, 4, 6, 8)
            .Concat(Roster(Role.Defensor, 5, 5.5f, 6, 6.5f, 7, 7.5f, 8, 8.5f))
            .Concat(Roster(Role.Midfielder, 4, 5, 6, 7, 8, 9, 10, 11))
            .Concat(Roster(Role.Forward, 3, 4, 5, 6, 7, 12))
            .ToList();

        _calculator.CalculatePoint(roster);

        var selected = roster.Where(x => x.CurrentPosition is >= FantaSoccerRole.GoalKeeper and <= FantaSoccerRole.Forward).ToList();
        Assert.Equal(11, selected.Count);
        Assert.Equal(1, selected.Count(x => x.Current.Role == Role.GoalKeeper));
        Assert.InRange(selected.Count(x => x.Current.Role == Role.Defensor), 3, 5);
        Assert.InRange(selected.Count(x => x.Current.Role == Role.Midfielder), 3, 5);
        Assert.InRange(selected.Count(x => x.Current.Role == Role.Forward), 1, 3);
        Assert.Equal(ExpectedBestValue(roster), selected.Sum(x => x.FinalValue.Value));
    }

    [Fact]
    public void BestFormationRespectsMaximumWhenOneRoleDominates()
    {
        var settings = LeagueSetting.Default;
        settings.Formation = FormationType.Best;
        _calculator.SetLeagueSettings(LeagueType.League, settings);
        var roster = Roster(Role.GoalKeeper, 4, 6, 8)
            .Concat(Roster(Role.Defensor, 93, 94, 95, 96, 97, 98, 99, 100))
            .Concat(Roster(Role.Midfielder, 20, 21, 22, 23, 24, 25, 26, 27))
            .Concat(Roster(Role.Forward, 10, 11, 12, 13, 14, 15))
            .ToList();

        _calculator.CalculatePoint(roster);

        var selected = roster.Where(x => x.CurrentPosition is >= FantaSoccerRole.GoalKeeper and <= FantaSoccerRole.Forward).ToList();
        Assert.Equal(5, selected.Count(x => x.Current.Role == Role.Defensor));
        Assert.Equal(4, selected.Count(x => x.Current.Role == Role.Midfielder));
        Assert.Equal(1, selected.Count(x => x.Current.Role == Role.Forward));
        Assert.Equal(ExpectedBestValue(roster), selected.Sum(x => x.FinalValue.Value));
    }

    [Fact]
    public void BestFormationSupportsRosterSmallerThanTwentyFive()
    {
        var settings = LeagueSetting.Default;
        settings.Formation = FormationType.Best;
        _calculator.SetLeagueSettings(LeagueType.League, settings);
        var roster = Roster(Role.GoalKeeper, 6, 7)
            .Concat(Roster(Role.Defensor, 5, 6, 7, 8, 9, 10))
            .Concat(Roster(Role.Midfielder, 4, 5, 6, 7, 8, 9, 10))
            .Concat(Roster(Role.Forward, 4, 5, 6, 7, 8))
            .ToList();

        _calculator.CalculatePoint(roster);

        var selected = roster.Where(x => x.CurrentPosition is >= FantaSoccerRole.GoalKeeper and <= FantaSoccerRole.Forward).ToList();
        Assert.Equal(11, selected.Count);
        Assert.Equal(ExpectedBestValue(roster), selected.Sum(x => x.FinalValue.Value));
    }

    [Fact]
    public void DoesNotCalculatePointForPlayersWithoutVotes()
    {
        _calculator.SetLeagueSettings(LeagueType.League, LeagueSetting.Default);

        var point = _calculator.CalculatePoint([_calculator.ForceEnrich(
            Player(Role.Forward), new Vote { HasVote = false }, null, null)]);

        Assert.Equal(0, point.Value);
        Assert.False(point.GoodPeople);
    }

    private static Player Player(Role role)
        => new() { Name = Guid.NewGuid().ToString(), Role = role, Position = FantaSoccerRole.Forward };

    private IEnumerable<EnrichedPlayer> Roster(Role role, params float[] values)
        => values.Select((value, index) => _calculator.ForceEnrich(new Player
        {
            Name = $"{role} {index}",
            Role = role,
            Position = FantaSoccerRole.Tribune
        }, new Vote { Value = value, HasVote = true }, null, null));

    private static float ExpectedBestValue(List<EnrichedPlayer> roster)
    {
        var goalkeeper = roster.Where(x => x.Current.Role == Role.GoalKeeper).Max(x => x.FinalValue.Value);
        return goalkeeper + (
            from defenders in Enumerable.Range(3, 3)
            from midfielders in Enumerable.Range(3, 3)
            from forwards in Enumerable.Range(1, 3)
            where defenders + midfielders + forwards == 10
            select Best(Role.Defensor, defenders) + Best(Role.Midfielder, midfielders) + Best(Role.Forward, forwards)
        ).Max();

        float Best(Role role, int count)
            => roster.Where(x => x.Current.Role == role)
                .OrderByDescending(x => x.FinalValue.Value)
                .Take(count)
                .Sum(x => x.FinalValue.Value);
    }
}
