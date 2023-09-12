using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FantacalcioV4
{
    public record Rank(string Id, List<RankedTeam> RankedTeams)
    {
        [JsonIgnore]
        public IOrderedEnumerable<IGrouping<int, RankedTeam>> OrderedRankedTeams
            => RankedTeams?.OrderByDescending(x => x.Point)?.ThenByDescending(x => x.ValuePoint).GroupBy(x => x.Group).OrderBy(x => x.Key);
        [JsonIgnore]
        public IOrderedEnumerable<IGrouping<int, RankedTeam>> FinancialOrderedRankedTeams
            => RankedTeams?.OrderByDescending(x => x.Money + x.PlusMoney)?.ThenByDescending(x => x.PlusMoney).GroupBy(x => x.Group).OrderBy(x => x.Key);
        public void AdjustRound(Daily daily)
        {
            foreach (var gamesByGroup in daily.Games)
            {
                foreach (var game in gamesByGroup.Value)
                {
                    var ranked = RankedTeams.FirstOrDefault(x => (x.Username == game.HomeUsername || x.Username == game.AwayUsername) && x.Group != gamesByGroup.Key);
                    if (ranked != default)
                    {
                        RankedTeams.Remove(ranked);
                        RankedTeams.Add(ranked with { Group = gamesByGroup.Key });
                    }
                }
            }
        }
        public void AddDay(Daily daily, LeagueSetting settings)
        {
            foreach (var gamesByGroup in daily.Games)
            {
                foreach (var game in gamesByGroup.Value)
                {
                    if (game.Result != default && game.Result.HasValue)
                    {
                        SetResult(game.Home);
                        SetResult(game.Away);
                    }

                    void SetResult(Team team)
                    {
                        if (team == default)
                            return;
                        var ranked = RankedTeams?.FirstOrDefault(x => x.TeamId == team.Id);
                        if (ranked == default)
                            RankedTeams.Add(ranked = new RankedTeam(team.Id, gamesByGroup.Key, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
                        ranked.AddValue(game, settings);
                    }
                }
            }
        }

        public void AddRank(Rank rank)
        {
            foreach (var ranked in rank.RankedTeams)
            {
                RankedTeam team = RankedTeams.FirstOrDefault(x => x.Username == ranked.Username);
                if (team != default)
                {
                    RankedTeams.Remove(team);
                    team += ranked;
                }
                RankedTeams.Add(team ?? ranked);
            }
        }
    }
}