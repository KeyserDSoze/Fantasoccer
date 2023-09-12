using System.Collections.Generic;

namespace FantacalcioV4
{
    public record Daily(int SerieADay, Dictionary<int, List<PlayedGame>> Games)
    {
        public void SetVote(List<Player> votes, LeagueSetting settings)
        {
            foreach (var group in Games)
            {
                foreach (var game in group.Value)
                {
                    if (game.Result == default || !game.Result.HasValue)
                        game.SetVote(votes, settings);
                }
            }
        }
    }
}