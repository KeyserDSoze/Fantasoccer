using System.Text.Json;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class LeagueSettingsTest
{
    [Fact]
    public void DefaultSettingsAreIndependentInstances()
    {
        var first = LeagueSetting.Default;
        var second = LeagueSetting.Default;
        first.PointForVictory = 99;
        first.Votes[Role.Undefined].Goal = 99;

        Assert.Equal(3, second.PointForVictory);
        Assert.Equal(3, second.Votes[Role.Undefined].Goal);
    }

    [Fact]
    public void SettingsRoundTripPreservesConfiguredRules()
    {
        var settings = LeagueSetting.Default;
        settings.Formation = FormationType.Best;
        settings.DelayedDay = 4;
        settings.Type = new LeagueTypeSettings
        {
            CalendarType = LeagueCalendarType.Cup,
            Rounds = [new LeagueRound { Name = "Final", Type = RoundType.Elimination, FromStart = false }],
            Numbers = new LeagueTypeNumberSettings { MaxPlayersInTeam = 18 }
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<LeagueSetting>(json);

        Assert.NotNull(restored);
        Assert.Equal(FormationType.Best, restored.Formation);
        Assert.Equal(4, restored.DelayedDay);
        Assert.Equal(LeagueCalendarType.Cup, restored.Type!.CalendarType);
        Assert.Equal("Final", Assert.Single(restored.Type.Rounds).Name);
        Assert.Equal(18, restored.Type.Numbers.MaxPlayersInTeam);
    }
}
