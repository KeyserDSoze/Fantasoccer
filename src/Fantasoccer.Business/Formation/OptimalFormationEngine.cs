namespace Fantasoccer.Business
{
    public sealed class OptimalFormationEngine : IOptimalFormationEngine
    {
        private static readonly Formation[] Formations =
        [
            new("3-4-3", 3, 4, 3),
            new("3-5-2", 3, 5, 2),
            new("4-3-3", 4, 3, 3),
            new("4-4-2", 4, 4, 2),
            new("4-5-1", 4, 5, 1),
            new("5-3-2", 5, 3, 2),
            new("5-4-1", 5, 4, 1)
        ];

        public OptimalFormationResult Calculate(OptimalFormationInput input)
        {
            ArgumentNullException.ThrowIfNull(input);
            var players = input.Players
                .Where(x => x != null && x.IsActive && x.Role != Role.Undefined)
                .Select(x => new Candidate(x, CalculateProjection(x, input.Projections)))
                .OrderByDescending(x => x.Projection)
                .ThenBy(x => x.Player.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (players.Count == 0)
                return new([], "", 0, 0, ["Nessun giocatore disponibile."]);

            var best = Formations
                .Select(formation => TryBuild(players, formation))
                .Where(x => x != null)
                .Cast<Solution>()
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Formation.Name, StringComparer.Ordinal)
                .FirstOrDefault();

            if (best == null)
                return new([], "", 0, 0, ["Rosa insufficiente per costruire una formazione valida."]);

            var confidence = best.Players.Count == 0
                ? 0
                : best.Players.Average(x => x.Confidence);
            var warnings = players.Count < 18
                ? new List<string> { "Rosa incompleta: la panchina è stata costruita con i giocatori disponibili." }
                : new List<string>();

            return new(
                best.Players.Select(x => new OptimalFormationPlayer(
                    x.Player.Key,
                    x.Player.Name,
                    x.Player.Role,
                    x.Position,
                    x.Projection,
                    x.Confidence)).ToList(),
                best.Formation.Name,
                best.Score,
                confidence,
                warnings);
        }

        private static Solution? TryBuild(IReadOnlyList<Candidate> players, Formation formation)
        {
            var selected = new List<SelectedCandidate>();
            foreach (var role in new[] { Role.GoalKeeper, Role.Defensor, Role.Midfielder, Role.Forward })
            {
                var count = role switch
                {
                    Role.GoalKeeper => 1,
                    Role.Defensor => formation.Defenders,
                    Role.Midfielder => formation.Midfielders,
                    Role.Forward => formation.Forwards,
                    _ => 0
                };
                var candidates = players.Where(x => x.Player.Role == role).Take(count).ToList();
                if (candidates.Count != count)
                    return null;
                selected.AddRange(candidates.Select((x, index) => new SelectedCandidate(x, ToPosition(role), index)));
            }

            var selectedKeys = selected.Select(x => x.Candidate.Player.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var bench = players
                .Where(x => !selectedKeys.Contains(x.Player.Key))
                .GroupBy(x => x.Player.Role)
                .SelectMany(x => x.Take(2))
                .Take(7)
                .Select((x, index) => new SelectedCandidate(x, BenchPosition(x.Player.Role, index), index))
                .ToList();
            var benchKeys = bench.Select(x => x.Player.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var tribune = players
                .Where(x => !selectedKeys.Contains(x.Player.Key) && !benchKeys.Contains(x.Player.Key))
                .Select(x => new SelectedCandidate(x, FantaSoccerRole.Tribune, 0));
            var result = selected.Concat(bench).Concat(tribune).ToList();
            var score = selected.Sum(x => x.Candidate.Projection);
            return new Solution(formation, result, score);
        }

        private static float CalculateProjection(Player player, IReadOnlyDictionary<string, OptimalPlayerProjection> projections)
        {
            projections.TryGetValue(player.Key, out var value);
            value ??= new OptimalPlayerProjection();
            var baseValue = value.FantaAverage > 0 ? value.FantaAverage : value.Average;
            var recent = value.RecentAverage > 0 ? value.RecentAverage : baseValue;
            var availability = Math.Clamp(value.Availability, 0, 1);
            return baseValue * 0.40f + recent * 0.25f + value.Matchup * 0.15f + value.HomeAway * 0.05f + value.Rank * 0.05f + availability * 0.10f;
        }

        private static FantaSoccerRole ToPosition(Role role) => role switch
        {
            Role.GoalKeeper => FantaSoccerRole.GoalKeeper,
            Role.Defensor => FantaSoccerRole.Defensor,
            Role.Midfielder => FantaSoccerRole.Midfielder,
            Role.Forward => FantaSoccerRole.Forward,
            _ => FantaSoccerRole.Tribune
        };

        private static FantaSoccerRole BenchPosition(Role role, int index) => role switch
        {
            Role.GoalKeeper => FantaSoccerRole.BackupGoalKeeper,
            Role.Defensor => index % 2 == 0 ? FantaSoccerRole.FirstBackupDefensor : FantaSoccerRole.SecondBackupDefensor,
            Role.Midfielder => index % 2 == 0 ? FantaSoccerRole.FirstBackupMidfielder : FantaSoccerRole.SecondBackupMidfielder,
            Role.Forward => index % 2 == 0 ? FantaSoccerRole.FirstBackupForward : FantaSoccerRole.SecondBackupForward,
            _ => FantaSoccerRole.Tribune
        };

        private sealed record Formation(string Name, int Defenders, int Midfielders, int Forwards);
        private sealed record Candidate(Player Player, float Projection)
        {
            public float Confidence => 0.5f;
        }
        private sealed record SelectedCandidate(Candidate Candidate, FantaSoccerRole Position, int Index)
        {
            public Player Player => Candidate.Player;
            public float Projection => Candidate.Projection;
            public float Confidence => Candidate.Confidence;
        }
        private sealed record Solution(Formation Formation, List<SelectedCandidate> Players, float Score);
    }
}
