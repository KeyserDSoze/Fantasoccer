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
        LeagueType LeagueType { get; }
        ValueTask BuildRequestAsync(int year, int day, LeagueType leagueType, LeagueSetting leagueSettings, bool withRealGame, bool withVote, bool withChance);
        void SetLeagueSettings(LeagueType leagueType, LeagueSetting leagueSettings);
        Point CalculatePoint(IEnumerable<Player> players);
        Point CalculatePoint(IEnumerable<EnrichedPlayer> players);
        EnrichedPlayer Enrich(Player player);
        EnrichedPlayer ForceEnrich(Player player, Vote? vote, Chance? chance, RealGame? realGame);
        IEnumerable<EnrichedPlayer> Enrich(IEnumerable<Player> players);
        VoteValue FinalValue(Player player);
        VoteValue FinalValue(EnrichedPlayer player);
    }
}
