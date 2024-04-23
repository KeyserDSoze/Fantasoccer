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
        void SetLeagueSettings(LeagueSetting leagueSettings);
        Point CalculatePoint(IEnumerable<Player> players);
        Point CalculatePoint(IEnumerable<EnrichedPlayer> players);
        EnrichedPlayer Enrich(Player player);
        EnrichedPlayer ForceEnrich(Player player, Vote? vote, Chance? chance, RealGame? realGame);
        IEnumerable<EnrichedPlayer> Enrich(IEnumerable<Player> players);
        IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<Player> players);
        IEnumerable<EnrichedPlayer> GetRightFormation(IEnumerable<EnrichedPlayer> players);
        VoteValue FinalValue(Player player);
        VoteValue FinalValue(EnrichedPlayer player);
        Task<bool> RecalculateDayAsync(int serieADay);
    }
}
