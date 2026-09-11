namespace Fantasoccer.Business
{
    public interface IOptimalFormationService
    {
        ValueTask<OptimalFormationResult> CalculateAsync(
            Team team,
            int year,
            int day,
            LeagueType leagueType,
            LeagueSetting leagueSetting,
            CancellationToken cancellationToken = default);
    }
}
