using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FantacalcioV4
{
    public record PlayedGame(string Id)
    {
        private string[] splittedId;
        [JsonIgnore]
        private string[] SplittedId => splittedId ??= Id.Split('/');
        [JsonIgnore]
        public string GroupName => SplittedId[1];
        [JsonIgnore]
        public int Year => int.Parse(SplittedId[3]);
        [JsonIgnore]
        public string League => SplittedId[5];
        [JsonIgnore]
        public int SerieADay => int.Parse(SplittedId[7]);
        [JsonIgnore]
        public string HomeUsername => SplittedId[9];
        [JsonIgnore]
        public string AwayUsername => SplittedId[11];
        [JsonIgnore]
        public string Uri => $"Game?id={Id}";
        private MiniTeam miniHome;
        [JsonIgnore]
        public MiniTeam MiniHome => miniHome ??= new MiniTeam(FantacalcioId.GetTeam(GroupName, Year, HomeUsername));
        private MiniTeam miniAway;
        [JsonIgnore]
        public MiniTeam MiniAway => miniAway ??= new MiniTeam(FantacalcioId.GetTeam(GroupName, Year, AwayUsername));
        private Team home;
        [JsonIgnore]
        public Team Home => home ??= TeamValue.Instance(MiniHome.Id, SerieADay);
        private Team away;
        [JsonIgnore]
        public Team Away => away ??= TeamValue.Instance(MiniAway.Id, SerieADay);

        public GameResult Result { get; set; }
        public GameResult CalculateResult(LeagueSetting settings)
        {
            Result = new(Home == default ? Point.Zero : Home.CalculatePoint(settings),
                Away == default ? Point.Zero : Away.CalculatePoint(settings));
            Result.CalculateGoals(settings);
            return Result;
        }

        public void SetVote(List<Player> votes, LeagueSetting settings)
        {
            bool validTeam = Home != default && Away != default;
            if (Home != default)
                Set(Home.ActivePlayers);
            if (Away != default)
                Set(Away.ActivePlayers);
            CalculateResult(settings);

            void Set(IEnumerable<Player> players)
            {
                foreach (var player in players)
                {
                    Player vote = votes.FirstOrDefault(x => x.Key == player.Key);
                    player.SetVote(vote, settings);
                }
            }
        }
        public void SetChances(List<Player> chancedPlayers)
        {
            if (Home != default)
                Set(Home.ActivePlayers);
            if (Away != default)
                Set(Away.ActivePlayers);

            void Set(IEnumerable<Player> players)
            {
                foreach (var player in players)
                {
                    Player chance = chancedPlayers.FirstOrDefault(x => x.Key == player.Key);
                    player.Chance = chance != default ? chance.Chance : Chance.Default();
                }
            }
        }
    }
}