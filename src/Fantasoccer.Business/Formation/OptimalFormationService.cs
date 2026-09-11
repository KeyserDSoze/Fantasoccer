using RepositoryFramework;
using Fantasoccer.Domain;

namespace Fantasoccer.Business
{
    internal sealed class OptimalFormationService : IOptimalFormationService
    {
        private readonly IOptimalFormationEngine _engine;
        private readonly ITeamCalculator _teamCalculator;
        private readonly IRepository<StatPlayersWrapper, int> _statsRepository;
        private readonly IRepository<RealRank, int> _rankRepository;

        public OptimalFormationService(
            IOptimalFormationEngine engine,
            ITeamCalculator teamCalculator,
            IRepository<StatPlayersWrapper, int> statsRepository,
            IRepository<RealRank, int> rankRepository)
        {
            _engine = engine;
            _teamCalculator = teamCalculator;
            _statsRepository = statsRepository;
            _rankRepository = rankRepository;
        }

        public async ValueTask<OptimalFormationResult> CalculateAsync(
            Team team,
            int year,
            int day,
            LeagueType leagueType,
            LeagueSetting leagueSetting,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(team);
            ArgumentNullException.ThrowIfNull(leagueSetting);
            cancellationToken.ThrowIfCancellationRequested();

            await _teamCalculator.BuildRequestAsync(year, day, leagueType, leagueSetting, true, true, true);
            var activePlayers = (team.Players ?? [])
                .Where(x => x.Status == PlayerInTeamStatus.Active)
                .ToList();
            var enrichedPlayers = _teamCalculator.Enrich(activePlayers).ToList();
            var stats = (await _statsRepository.GetAsync(year, cancellationToken))?.Players ?? [];
            var rank = (await _rankRepository.GetAsync(year, cancellationToken))?.Teams ?? [];
            var projections = enrichedPlayers
                .Where(x => !string.IsNullOrWhiteSpace(x.Current.Key))
                .GroupBy(x => x.Current.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                x => x.Key,
                x => ToProjection(x.First(), stats, rank),
                StringComparer.OrdinalIgnoreCase);

            return _engine.Calculate(new OptimalFormationInput
            {
                Players = activePlayers,
                Projections = projections
            });
        }

        private static OptimalPlayerProjection ToProjection(
            EnrichedPlayer player,
            IReadOnlyList<StatPlayer> stats,
            IReadOnlyList<RealRankedTeam> rank)
        {
            var stat = stats.FirstOrDefault(x => string.Equals(x.Key, player.Current.Key, StringComparison.OrdinalIgnoreCase));
            var vote = player.Vote?.Value ?? 0;
            var availability = player.Chance?.Status switch
            {
                ChanceType.Injury or ChanceType.Disqualified => 0,
                ChanceType.Maybe => 0.5f,
                _ => 1
            };
            var average = stat?.Average ?? 0;
            var fantaAverage = stat?.FantaAverage ?? 0;
            var recentAverage = stat?.AllGames?
                .Where(x => x.Vote.HasValue)
                .OrderByDescending(x => x.SerieADay)
                .Take(5)
                .Select(x => x.Vote!.Value)
                .DefaultIfEmpty(0)
                .Average() ?? 0;
            var expectedVote = vote > 0 ? vote : (fantaAverage > 0 ? fantaAverage : average > 0 ? average : 5.5f);
            var matchup = CalculateMatchup(player, rank);
            var confidence = stat != null && player.Chance != null && player.Vote != null ? 1f : stat != null || player.Vote != null ? 0.65f : 0.25f;
            var finalValue = player.FinalValue?.Value ?? 0;
            return new OptimalPlayerProjection(
                Average: average > 0 ? average : expectedVote,
                FantaAverage: finalValue > 0 ? finalValue : fantaAverage > 0 ? fantaAverage : expectedVote,
                RecentAverage: recentAverage > 0 ? recentAverage : expectedVote,
                Availability: availability,
                Matchup: matchup,
                HomeAway: player.RealGame == null ? 0 : IsHome(player) ? 0.15f : -0.05f,
                Rank: RankValue(player, rank),
                Confidence: confidence);
        }

        private static float CalculateMatchup(EnrichedPlayer player, IReadOnlyList<RealRankedTeam> rank)
        {
            if (player.RealGame == null)
                return 0;
            var ownName = player.Current.RealTeam?.Name;
            var opponent = IsHome(player) ? player.RealGame.Away?.Name : player.RealGame.Home?.Name;
            var own = rank.FirstOrDefault(x => string.Equals(x.Name, ownName, StringComparison.OrdinalIgnoreCase))?.Point;
            var other = rank.FirstOrDefault(x => string.Equals(x.Name, opponent, StringComparison.OrdinalIgnoreCase))?.Point;
            return own.HasValue && other.HasValue ? Math.Clamp((own.Value - other.Value) / 20f, -1, 1) : 0;
        }

        private static float RankValue(EnrichedPlayer player, IReadOnlyList<RealRankedTeam> rank)
        {
            var point = rank.FirstOrDefault(x => string.Equals(x.Name, player.Current.RealTeam?.Name, StringComparison.OrdinalIgnoreCase))?.Point;
            return point.HasValue ? Math.Clamp(point.Value / 100f, -1, 1) : 0;
        }

        private static bool IsHome(EnrichedPlayer player)
            => string.Equals(player.RealGame?.Home?.Name, player.Current.RealTeam?.Name, StringComparison.OrdinalIgnoreCase);
    }
}
