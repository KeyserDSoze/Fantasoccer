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

    [Fact]
    public void PostponementPolicyDefaultsToPoliticalSixesAndOlderSettingsKeepThatDefault()
    {
        var settings = LeagueSetting.Default;
        Assert.False(settings.Postponements.WaitForRecovery);
        Assert.All(new[] { Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward }, role => Assert.Equal(6, settings.Postponements.ValueFor(role)));
        settings.Postponements = new PostponementSettings { WaitForRecovery = true, Goalkeeper = 7, Defender = 5.5f, Midfielder = 6.5f, Forward = 5 };

        var restored = JsonSerializer.Deserialize<LeagueSetting>(JsonSerializer.Serialize(settings))!;
        Assert.True(restored.Postponements.WaitForRecovery);
        Assert.Equal(new[] { 7, 5.5f, 6.5f, 5 }, new[] { Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward }.Select(restored.Postponements.ValueFor));

        // Settings saved before this policy existed have no "pp" member.
        var legacy = JsonSerializer.SerializeToNode(LeagueSetting.Default)!.AsObject();
        legacy.Remove("pp");
        var old = JsonSerializer.Deserialize<LeagueSetting>(legacy.ToJsonString())!;
        Assert.False(old.Postponements.WaitForRecovery); Assert.Equal(6, old.Postponements.ValueFor(Role.Forward));
    }
}
