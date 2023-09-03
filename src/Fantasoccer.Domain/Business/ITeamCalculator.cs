namespace Fantasoccer.Business
{
    public interface ITeamCalculator
    {
        bool IsLive { get; }
        bool IsEditable { get; }
        int SerieADay { get; }
        VotedRealPlayerWrapper? Live { get; }
        VotedRealPlayerWrapper? Official { get; }
        ChancedRealPlayerWrapper? Chance { get; }
        RealDay? RealDay { get; }
        LeagueSetting? LeagueSetting { get; }
        ValueTask BuildRequestAsync(int year, int day, LeagueSetting leagueSettings, bool withRealGame, bool withVote, bool withChance);
        Point CalculatePoint(IEnumerable<EnrichedPlayer> players);
        IEnumerable<EnrichedPlayer> Enrich(IEnumerable<Player> players);
        IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<EnrichedPlayer> players);
        VoteValue FinalValue(EnrichedPlayer player);
    }
}
