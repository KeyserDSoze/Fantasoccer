using System.Collections.Generic;

namespace FantacalcioV4
{
    public record PlayedAnnualLeague(string Id, LeagueType Type, LeagueSetting Settings, List<Team> Teams) : MiniLeague(Id, Type)
    {
        public void AddMoney(Rank rank, List<Transfer> transfers = default)
        {
            foreach (var team in Teams)
            {
                //todo da aggiungere i trasferimenti
                if (transfers != default)
                    team.AddMoneyForTransfer(transfers);
                team.AddMoneyFromRank(rank);
            }
        }
    }
}