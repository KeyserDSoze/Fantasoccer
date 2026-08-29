using Fantasoccer.Business;

namespace Fantasoccer.Tests.Tests.Rules;

public sealed class TeamCheckerRulesTest
{
    private readonly ITeamChecker _checker;

    public TeamCheckerRulesTest(ITeamChecker checker)
        => _checker = checker;

    [Fact]
    public void AcceptsValidThreeFourThreeFormation()
    {
        var result = _checker.Validate(Team(
            Players(Role.GoalKeeper, FantaSoccerRole.GoalKeeper, 1)
                .Concat(Players(Role.Defensor, FantaSoccerRole.Defensor, 3))
                .Concat(Players(Role.Midfielder, FantaSoccerRole.Midfielder, 4))
                .Concat(Players(Role.Forward, FantaSoccerRole.Forward, 3))));

        Assert.True(result.IsOk);
    }

    [Fact]
    public void RejectsFormationWithFewerThanElevenStarters()
    {
        var result = _checker.Validate(Team(Players(Role.Forward, FantaSoccerRole.Forward, 10)));

        Assert.False(result.IsOk);
        Assert.Contains("Meno di 11", result.Message);
    }

    [Fact]
    public void RejectsPlayerDeployedInWrongRole()
    {
        var players = Players(Role.GoalKeeper, FantaSoccerRole.GoalKeeper, 1)
            .Concat(Players(Role.Defensor, FantaSoccerRole.Defensor, 3))
            .Concat(Players(Role.Midfielder, FantaSoccerRole.Midfielder, 4))
            .Concat(Players(Role.Forward, FantaSoccerRole.Forward, 3))
            .ToList();
        players[1].Role = Role.Midfielder;

        var result = _checker.Validate(Team(players));

        Assert.False(result.IsOk);
        Assert.Equal("Giocatore fuori ruolo.", result.Message);
    }

    [Fact]
    public void RejectsMoreThanOnePlayerInSameBackupSlot()
    {
        var players = Players(Role.GoalKeeper, FantaSoccerRole.GoalKeeper, 1)
            .Concat(Players(Role.Defensor, FantaSoccerRole.Defensor, 3))
            .Concat(Players(Role.Midfielder, FantaSoccerRole.Midfielder, 4))
            .Concat(Players(Role.Forward, FantaSoccerRole.Forward, 3))
            .Concat(Players(Role.Forward, FantaSoccerRole.FirstBackupForward, 2));

        var result = _checker.Validate(Team(players));

        Assert.False(result.IsOk);
    }

    private static Team Team(IEnumerable<Player> players)
        => new() { Name = "Team", Owner = "owner", AdditionalOwners = [], Players = [.. players] };

    private static IEnumerable<Player> Players(Role role, FantaSoccerRole position, int count)
        => Enumerable.Range(1, count).Select(index => new Player
        {
            Name = $"{role} {position} {index}",
            Role = role,
            Position = position,
            Status = PlayerInTeamStatus.Active
        });
}
