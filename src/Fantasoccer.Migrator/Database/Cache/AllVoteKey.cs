using Rystem.Business;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public class AllVoteKey : ICacheKey<List<SummedVotesPlayer>>
    {
        public int Year { get; set; } = FantacalcioTime.ActualYear;
        public async Task<List<SummedVotesPlayer>> FetchAsync()
        {
            int days = 36;
            if (Year == FantacalcioTime.ActualYear)
            {
                var calendar = await new RealCalendarKey().InstanceAsync();
                days = calendar.LastDay.SerieADay;
            }
            var players = await new AllPlayerKey() { Year = Year }.InstanceAsync();
            List<SummedVotesPlayer> finalPlayers = new();
            foreach (var player in players)
                finalPlayers.Add(new SummedVotesPlayer(player.Key, new(new())));
            for (int day = 1; day <= days; day++)
            {
                var votes = await new FinalVotesContext(Year, day).GetAsync();
                if (votes == default)
                    break;
                foreach (var vote in votes)
                {
                    SummedVotesPlayer finalPlayer = finalPlayers.FirstOrDefault(x => x.Key == vote.Key);
                    if (finalPlayer != default)
                        finalPlayer.AddVote(vote.Vote);
                }
            }
            return finalPlayers;
        }
    }
}