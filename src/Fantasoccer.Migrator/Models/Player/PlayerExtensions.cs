using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FantacalcioV4
{
    public static class VotedPlayerExtensions
    {
        public static List<Player> MergeVote(this List<Player> voted, List<Player> toMerge)
        {
            if (toMerge == default || toMerge.Count == 0)
                return voted;
            List<Player> merged = new();
            foreach (var vote in voted)
                merged.Add(vote);
            foreach (var merging in toMerge)
            {
                if (!merged.Any(x => x.Key == merging.Key))
                    merged.Add(merging);
            }
            return merged;
        }
    }
}