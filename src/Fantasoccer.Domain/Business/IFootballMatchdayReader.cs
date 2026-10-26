namespace Fantasoccer.Business;

// A calculation input, not a generic persistence abstraction. SQL and the legacy
// regression adapter load the same characterized scoring engine independently.
public sealed record FootballMatchdayInput(int Day, bool IsLive, bool IsEditable, RealDay? RealDay,
    VotedRealPlayerWrapper? Live, VotedRealPlayerWrapper? Official, ChancedRealPlayerWrapper? Chances);

public interface IFootballMatchdayReader
{
    Task<FootballMatchdayInput> ReadAsync(int year, int day, bool withVotes, bool withChances,
        CancellationToken cancellationToken = default);
}
