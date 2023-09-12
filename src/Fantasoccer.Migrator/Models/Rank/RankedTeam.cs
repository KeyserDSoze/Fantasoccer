using System.Linq;
using System.Text.Json.Serialization;

namespace FantacalcioV4
{
    public record RankedTeam
    {
        public RankedTeam(string teamId, int group, int point, int victories, int draws, int defeats,
        int goal, int sufferedGoal, float valuePoint, float sufferedValuePoint, int plusMoney, int money)
        {
            TeamId = teamId;
            Group = group;
            Point = point;
            Victories = victories;
            Draws = draws;
            Defeats = defeats;
            Goal = goal;
            SufferedGoal = sufferedGoal;
            ValuePoint = valuePoint;
            SufferedValuePoint = sufferedValuePoint;
            PlusMoney = plusMoney;
            Money = money;
        }
        public string TeamId { get; private set; }
        [JsonIgnore]
        public string Username => TeamId.Split('/').Last();
        public int Group { get; set; }
        private string name;
        [JsonIgnore]
        public string Name => name ??= TeamValue.NameInstance(TeamId);
        public int Point { get; private set; }
        public int Victories { get; private set; }
        public int Draws { get; private set; }
        public int Defeats { get; set; }
        public int Goal { get; set; }
        public int SufferedGoal { get; set; }
        public float ValuePoint { get; set; }
        public float SufferedValuePoint { get; set; }
        public int PlusMoney { get; set; }
        public int Money { get; set; }
        public bool AddValue(PlayedGame game, LeagueSetting leagueSetting)
        {
            if (game.Home?.Id != TeamId && game.Away?.Id != TeamId)
                return false;
            float pointTeam = 0F;
            float pointOpponent = 0F;
            int goalTeam = 0;
            int goalOpponent = 0;
            var result = game.Result ?? game.CalculateResult(leagueSetting);
            if (result == default || !result.HasValue)
                return false;
            var goals = result.CalculateGoals(leagueSetting);
            if (game.Home != default && game.Home.Id == TeamId)
                SetPointAndGoal(result.Home.Value, goals.Home, result.Away.Value, goals.Away);
            else if(game.Away != default && game.Away.Id == TeamId)
                SetPointAndGoal(result.Away.Value, goals.Away, result.Home.Value, goals.Home);

            void SetPointAndGoal(float point, int goal, float opponentPoint, int opponentGoal)
            {
                pointTeam = point;
                pointOpponent = opponentPoint;
                goalTeam = goal;
                goalOpponent = opponentGoal;
            }

            this.Goal += goalTeam;
            this.SufferedGoal += goalOpponent;
            this.ValuePoint += pointTeam;
            this.SufferedValuePoint += pointOpponent;
            if (goalTeam > goalOpponent)
            {
                this.Victories++;
                this.Point += leagueSetting.PointForVictory;
            }
            else if (goalOpponent > goalTeam)
            {
                this.Defeats++;
                this.Point += leagueSetting.PointForDefeat;
            }
            else
            {
                this.Draws++;
                this.Point += leagueSetting.PointForDraw;
            }
            this.PlusMoney += leagueSetting.MoneyForGoal * goalTeam;
            this.PlusMoney += leagueSetting.MoneyForSufferedGoal * goalOpponent;
            return true;
        }
        //todo: Devo mettere le coppe che ha vinto

        public static RankedTeam operator +(RankedTeam a, RankedTeam b)
            => new(a.TeamId, a.Group,
                a.Point + b.Point,
                a.Victories + b.Victories,
                a.Draws + b.Draws,
                a.Defeats + b.Defeats,
                a.Goal + b.Goal,
                a.SufferedGoal + b.SufferedGoal,
                a.ValuePoint + b.ValuePoint,
                a.SufferedValuePoint + b.SufferedValuePoint,
                a.PlusMoney + b.PlusMoney,
                a.Money + b.Money
                );
    }
}