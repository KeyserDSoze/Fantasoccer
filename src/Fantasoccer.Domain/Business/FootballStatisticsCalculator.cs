using Fantasoccer.Domain;

namespace Fantasoccer.Business;

public static class FootballStatisticsCalculator
{
    public static StatPlayersWrapper Calculate(IReadOnlyList<RealPlayer> catalogue, int until, IReadOnlyDictionary<int, VotedRealPlayerWrapper> votes, ITeamCalculator calculator)
    {
        calculator.SetLeagueSettings(LeagueType.League, LeagueSetting.Default);
        var result = new StatPlayersWrapper { Players = catalogue.Select(x => new StatPlayer { Name = x.Name, RealTeam = x.RealTeam,
            Role = x.Role, IsActive = x.IsActive, Visible = x.Visible, AllGames = [] }).ToList() };
        for (var day = until; day > 0; day--)
            foreach (var player in result.Players)
            {
                var voted = votes.GetValueOrDefault(day)?.Players.FirstOrDefault(x => x.Key == player.Key);
                var vote = voted?.Vote;
                var game = new StatPlayerGame { SerieADay = day, Vote = vote?.HasVote == true ? vote.Value : null, Positiveness = 0 };
                player.AllGames!.Add(game);
                if (vote == null) { player.NoPlayed++; game.Positiveness = -2; continue; }
                if (!vote.HasVote) { player.WithoutVote++; game.Positiveness = -2; continue; }
                player.WithVote++; player.Summatory += vote.Value;
                player.Penalty += vote.Penalty; if (vote.Penalty > 0) game.Positiveness += 2;
                player.SufferedGoal += vote.SufferedGoal; if (vote.SufferedGoal > 0) game.Positiveness--;
                if (player.Role == Role.GoalKeeper && vote.SufferedGoal == 0) game.Positiveness++;
                player.StoppedPenalty += vote.StoppedPenalty; if (vote.StoppedPenalty > 0) game.Positiveness += 2;
                player.Assist += vote.Assist; if (vote.Assist > 0) game.Positiveness++;
                player.OwnGoal += vote.OwnGoal; if (vote.OwnGoal > 0) game.Positiveness -= 2;
                player.Goal += vote.Goal; if (vote.Goal > 0) game.Positiveness += 2;
                player.WrongedPenalty += vote.WrongedPenalty; if (vote.WrongedPenalty > 0) game.Positiveness -= 2;
                if (vote.Status == Behaviour.RedCard) { player.RedCard++; game.Positiveness -= 2; }
                if (vote.Status == Behaviour.YellowCard) { player.YellowCard++; game.Positiveness--; }
                if (vote.Value >= 6) { player.IsEnough++; if (player.Role is Role.GoalKeeper or Role.Defensor) game.Positiveness++; }
                if (vote.ManOfTheMatch) { player.ManOfTheMatch++; game.Positiveness++; }
                if (vote.Injured) { player.Injured++; game.Positiveness -= 2; }
                var value = calculator.FinalValue(calculator.ForceEnrich(new Player { Role = player.Role }, vote, null, null));
                player.FantaSummatory += value.Value;
                if (value.Special) { player.WithSpecial++; game.Positiveness++; }
            }
        return result;
    }
}
