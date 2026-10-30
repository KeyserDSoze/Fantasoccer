namespace Fantasoccer.Business;

// A calculation input, not a generic persistence abstraction.
public sealed record FootballMatchdayInput(int Day, bool IsLive, bool IsEditable, RealDay? RealDay,
    VotedRealPlayerWrapper? Live, VotedRealPlayerWrapper? Official, ChancedRealPlayerWrapper? Chances)
{
    // Old snapshots remain available for display, but must not exclude players from a new suggested formation.
    public bool ChancesAreStale { get; init; }
}

public interface IFootballMatchdayReader
{
    Task<FootballMatchdayInput> ReadAsync(int year, int day, bool withVotes, bool withChances,
        CancellationToken cancellationToken = default);
}
