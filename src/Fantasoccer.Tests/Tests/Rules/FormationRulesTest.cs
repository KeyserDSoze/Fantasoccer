using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class FormationRulesTest
{
    private readonly ILeagueCalculator _league;

    public FormationRulesTest(IFactory<ILeagueCalculator> factory)
        => _league = factory.Create(LeagueType.League.ToString())!;

    [Fact]
    public void ReplacesUnvotedStarterWithVotedBackupOfSameRole()
    {
        var starter = Enriched("Starter", Role.Forward, FantaSoccerRole.Forward, null);
        var backup = Enriched("Backup", Role.Forward, FantaSoccerRole.FirstBackupForward, 6);

        var formation = _league.GetRightFormation([starter, backup]).ToList();

        Assert.Equal(FantaSoccerRole.FirstBackupForward, starter.CurrentPosition);
        Assert.Equal(FantaSoccerRole.Forward, backup.CurrentPosition);
        Assert.Equal("Backup", formation.Single(x => x.CurrentPosition == FantaSoccerRole.Forward).Current.Name);
    }

    [Fact]
    public void DoesNotReplaceStarterWithDifferentRole()
    {
        var starter = Enriched("Starter", Role.Forward, FantaSoccerRole.Forward, null);
        var backup = Enriched("Backup", Role.Midfielder, FantaSoccerRole.FirstBackupMidfielder, 6);

        var formation = _league.GetRightFormation([starter, backup]).ToList();

        Assert.Equal(FantaSoccerRole.Forward, starter.CurrentPosition);
        Assert.Contains(formation, x => x.Current.Name == "Starter");
    }

    [Fact]
    public void KeepsVotedStarterInField()
    {
        var starter = Enriched("Starter", Role.Forward, FantaSoccerRole.Forward, 5);
        var backup = Enriched("Backup", Role.Forward, FantaSoccerRole.FirstBackupForward, 8);

        var formation = _league.GetRightFormation([starter, backup]).ToList();

        Assert.Equal(FantaSoccerRole.Forward, starter.CurrentPosition);
        Assert.Equal(FantaSoccerRole.FirstBackupForward, backup.CurrentPosition);
        Assert.Equal("Starter", formation.Single(x => x.CurrentPosition == FantaSoccerRole.Forward).Current.Name);
    }

    private static EnrichedPlayer Enriched(string name, Role role, FantaSoccerRole position, float? vote)
        => new()
        {
            Current = new Player { Name = name, Role = role, Position = position },
            CurrentPosition = position,
            Vote = vote.HasValue ? new Vote { Value = vote.Value, HasVote = true } : null
        };
}
