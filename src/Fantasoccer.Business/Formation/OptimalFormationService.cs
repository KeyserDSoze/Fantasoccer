using Fantasoccer.Domain;

namespace Fantasoccer.Business
{
    internal sealed class OptimalFormationService : IOptimalFormationService
    {
        private readonly IOptimalFormationEngine _engine;
        private readonly ITeamCalculator _teamCalculator;
        private readonly IFormationStatisticsReader _statistics;

        public OptimalFormationService(
            IOptimalFormationEngine engine,
            ITeamCalculator teamCalculator,
            IFormationStatisticsReader statistics)
        {
            _engine = engine;
            _teamCalculator = teamCalculator;
            _statistics = statistics;
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
            var useChances = !_teamCalculator.ChancesAreStale;
            var hasProbableLineupData = useChances && enrichedPlayers.Any(x =>
                x.Chance != null && (x.Chance.Fantagazzetta || x.Chance.Gazzetta));
            var statistics = await _statistics.ReadAsync(year, cancellationToken);
            var stats = statistics.Players;
            var rank = statistics.Clubs;
            var projections = enrichedPlayers
                .Where(x => x.Current != null && !string.IsNullOrWhiteSpace(x.Current.Key))
                .GroupBy(x => x.Current.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                x => x.Key,
                x => ToProjection(x.First(), stats, rank, hasProbableLineupData, useChances),
                StringComparer.OrdinalIgnoreCase);

            var result = _engine.Calculate(new OptimalFormationInput
            {
                Players = activePlayers,
                Projections = projections
            });
            return useChances ? result : result with { Warnings = result.Warnings.Concat([
                "Probabili formazioni non aggiornate: suggerimento basato su statistiche e voti disponibili."]).ToArray() };
        }

        private static OptimalPlayerProjection ToProjection(
            EnrichedPlayer player,
            IReadOnlyList<StatPlayer> stats,
            IReadOnlyList<RealRankedTeam> rank,
            bool hasProbableLineupData, bool useChances)
        {
            var stat = stats.FirstOrDefault(x => string.Equals(x.Key, player.Current.Key, StringComparison.OrdinalIgnoreCase));
            var vote = player.Vote?.Value ?? 0;
            var chance = useChances ? player.Chance : null;
            var availability = chance?.Status switch
            {
                ChanceType.Injury or ChanceType.Disqualified => 0,
                ChanceType.Maybe => 0.5f,
                _ => 1
            };
            if (hasProbableLineupData && chance != null &&
                !chance.Fantagazzetta && !chance.Gazzetta)
                availability = 0;
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
            var confidence = stat != null && chance != null && player.Vote != null ? 1f : stat != null || player.Vote != null ? 0.65f : 0.25f;
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
            var ownTeam = rank.FirstOrDefault(x => string.Equals(x.Name, ownName, StringComparison.OrdinalIgnoreCase));
            var opponentTeam = rank.FirstOrDefault(x => string.Equals(x.Name, opponent, StringComparison.OrdinalIgnoreCase));
            if (ownTeam == null || opponentTeam == null)
                return 0;

            var ownTier = TierOf(ownTeam.Name, rank);
            var opponentTier = TierOf(opponentTeam.Name, rank);
            var tierAdvantage = (opponentTier - ownTier) / 3f;
            var pointsAdvantage = Math.Clamp((ownTeam.Point - opponentTeam.Point) / 30f, -1, 1);
            var homeAdvantage = IsHome(player) ? 0.15f : -0.15f;
            return Math.Clamp(tierAdvantage * 0.7f + pointsAdvantage * 0.3f + homeAdvantage, -1, 1);
        }

        private static float RankValue(EnrichedPlayer player, IReadOnlyList<RealRankedTeam> rank)
        {
            var team = rank.FirstOrDefault(x => string.Equals(x.Name, player.Current.RealTeam?.Name, StringComparison.OrdinalIgnoreCase));
            if (team == null)
                return 0;

            var tier = TierOf(team.Name, rank);
            return Math.Clamp((4 - tier) / 3f, -1, 1);
        }

        private static int TierOf(string? teamName, IReadOnlyList<RealRankedTeam> rank)
        {
            if (string.IsNullOrWhiteSpace(teamName))
                return 4;

            var position = rank
                .Select((team, index) => new { team, index })
                .FirstOrDefault(x => string.Equals(x.team.Name, teamName, StringComparison.OrdinalIgnoreCase))?.index;
            return position.HasValue ? Math.Min(position.Value / 5 + 1, 4) : 4;
        }

        private static bool IsHome(EnrichedPlayer player)
            => string.Equals(player.RealGame?.Home?.Name, player.Current.RealTeam?.Name, StringComparison.OrdinalIgnoreCase);
    }
}
