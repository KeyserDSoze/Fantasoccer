using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Fantasoccer
{
    public record MiniTeam(string Id)
    {
        private string name;
        [JsonIgnore]
        public string Name => name ??= TeamValue.NameInstance(Id);
        private string[] splittedId;
        [JsonIgnore]
        private string[] SplittedId => splittedId ??= Id.Split('/');
        [JsonIgnore]
        public string Username => SplittedId[5];
        [JsonIgnore]
        public string GroupName => SplittedId[1];
        [JsonIgnore]
        public int Year => int.Parse(SplittedId[3]);
    }
    //Group/GroupId/Year/YearId/User/UserId
    public record Team(string Id, List<Player> Players) : MiniTeam(Id)
    {
        [JsonIgnore]
        public IEnumerable<Player> ActivePlayers => Players.Where(x => x.TeamStatus == PlayerInTeamStatus.Active);
        [JsonIgnore]
        public IEnumerable<Player> NotSoldActivePlayers => ActivePlayers.Where(x => x.Value != default);
        [JsonIgnore]
        public int RevenueMoney { get; set; }
        [JsonIgnore]
        public int MoneyFromRank { get; set; }
        [JsonIgnore]
        public int MoneyFromSoldWithNoReturnedPrice => Players.Where(x => x.TeamStatus == PlayerInTeamStatus.SoldWithNoReturnedPrice).Sum(x => x.Price);
        [JsonIgnore]
        public int MoneyFromSoldWithOneHalfReturnedPrice => Players.Where(x => x.TeamStatus == PlayerInTeamStatus.SoldForOneHalf).Sum(x => x.Price / 2);
        [JsonIgnore]
        public int TotalCostForPlayers => Players.Sum(x => x.Price);
        [JsonIgnore]
        public int TotalMoney => 1000 - TotalCostForPlayers + RevenueMoney + MoneyFromRank + MoneyFromSoldWithOneHalfReturnedPrice;
        public void AddMoneyForTransfer(List<Transfer> transfers)
        {
            foreach (Transfer transfer in transfers)
            {
                if (transfer.ATeam.Id == Id)
                    RevenueMoney -= transfer.Price;
                else if (transfer.BTeam.Id == Id)
                    RevenueMoney += transfer.Price;
            }
        }
        public void AddMoneyFromRank(Rank rank)
        {
            var team = rank.RankedTeams.FirstOrDefault(x => x.TeamId == Id);
            if (team != default)
                MoneyFromRank = team.PlusMoney;
        }
        public (List<Player> InField, List<Player> Substitutes) GetRightFormation()
        {
            if (this.Players.Count == 0)
                return (new List<Player>(), new List<Player>());
            List<Player> allPlayers = ActivePlayers.OrderBy(x => x.Position.Status).ToList();
            List<Player> playersInField = ActivePlayers.OrderBy(x => x.Position.Status).Where(x => x.Position.Status >= FantaSoccerRole.GoalKeeper && x.Position.Status <= FantaSoccerRole.Forward).ToList();
            List<Player> playersAsSubstitute = ActivePlayers.OrderBy(x => x.Position.Status).Where(x => x.Position.Status > FantaSoccerRole.Forward && x.Position.Status <= FantaSoccerRole.SecondBackupForward).ToList();
            for (int i = 0; i < playersInField.Count; i++)
            {
                if (playersInField[i].Position.Status >= FantaSoccerRole.GoalKeeper &&
                    playersInField[i].Position.Status <= FantaSoccerRole.Forward)
                {
                    if (playersInField[i].Vote != default && !playersInField[i].Vote.HasVote)
                    {
                        for (int j = 0; j < playersAsSubstitute.Count; j++)
                        {
                            if (playersAsSubstitute[j].Role == playersInField[i].Role && playersAsSubstitute[j].Vote.HasVote)
                            {
                                Position positionSubstitute = new(playersAsSubstitute[j].Position.X, playersAsSubstitute[j].Position.Y);
                                Position positionInField = new(playersInField[i].Position.X, playersInField[i].Position.Y);
                                playersInField[i].PositionBeforeChange = playersInField[i].Position;
                                playersAsSubstitute[j].PositionBeforeChange = playersAsSubstitute[j].Position;
                                var switcher = playersInField[i];
                                playersInField[i] = playersAsSubstitute[j];
                                playersAsSubstitute[j] = switcher;
                                playersInField[i].Position = positionInField;
                                playersAsSubstitute[j].Position = positionSubstitute;
                                break;
                            }
                        }
                    }
                }
            }
            return (playersInField, playersAsSubstitute);
        }
        public Point CalculatePoint(LeagueSetting leagueSetting)
        {
            if (this.Players.Count == 0)
                return new Point(0F, false, false);
            float point = 0F;
            bool defensiveBonus = true;
            int defensorCount = 0;
            //todo: da pensare all'anno nuovo, qui se arrivano a chiedere partite senza giocatori deve tornare zero
            foreach (var player in GetRightFormation().InField)
            {
                if (player.Vote != default && player.Vote.HasVote)
                {
                    point += player.Vote.FinalValue(player.Role, leagueSetting).Value;
                    if (player.Role == Role.Defensor)
                        defensorCount++;
                }
                if (player.Role == Role.Defensor && (player.Vote == default || !player.Vote.HasVote || player.Vote.Value < 6))
                    defensiveBonus = false;
            }
            if (defensiveBonus)
            {
                if (defensorCount == 3)
                    point += leagueSetting.PointForStrongDefense;
                else if (defensorCount == 4)
                    point += leagueSetting.PointForStrongDefense4;
                else if (defensorCount == 5)
                    point += leagueSetting.PointForStrongDefense5;
            }
            return new Point(point, defensiveBonus,
                leagueSetting.PointForOwnGoal > 0 && point < leagueSetting.PointForFirstGoal - leagueSetting.PointForOwnGoal);
        }
    }
}