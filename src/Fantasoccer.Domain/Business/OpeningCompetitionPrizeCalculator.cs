namespace Fantasoccer.Business.Services
{
    public sealed record OpeningCompetitionPrizeRule(int Position, int Credits);

    public sealed class OpeningCompetitionPrizeCalculator
    {
        public static int GetPrize(IEnumerable<OpeningCompetitionPrizeRule> rules, float score, IReadOnlyList<(string Owner, float Score)> standings)
        {
            var ordered = standings.OrderByDescending(x => x.Score).ToList();
            var index = ordered.FindIndex(x => x.Score == score);
            if (index < 0)
                return 0;

            var position = ordered.Count(x => x.Score > score) + 1;
            return rules.FirstOrDefault(x => x.Position == position)?.Credits ?? 0;
        }

        public static IReadOnlyDictionary<string, int> Calculate(IEnumerable<OpeningCompetitionPrizeRule> rules, IEnumerable<(string Owner, float Score)> standings)
        {
            var ordered = standings.OrderByDescending(x => x.Score).ToList();
            var prizes = rules.ToDictionary(x => x.Position, x => x.Credits);
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var index = 0; index < ordered.Count; index++)
            {
                var position = ordered.Count(x => x.Score > ordered[index].Score) + 1;
                result[ordered[index].Owner] = prizes.GetValueOrDefault(position);
            }
            return result;
        }
    }
}
