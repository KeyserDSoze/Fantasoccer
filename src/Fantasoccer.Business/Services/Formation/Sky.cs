using System.Text.Json;
using System.Text.RegularExpressions;

namespace Fantasoccer.Business.IngestionData
{
    internal class Sky : IFormationParser
    {
        private static readonly Regex s_playersRegex = new(@"<span[\s\S]*?<\/span>");
        private readonly HttpClient _httpClient;
        public Sky(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
        }
        public async Task<List<ChancedRealPlayer>> GetPlayersAsync()
        {
            List<ChancedRealPlayer> players = new();
            var returnedValue = await _httpClient.GetStringAsync("https://sport.sky.it/calcio/serie-a/probabili-formazioni").NoContext();
            var json = returnedValue.Split("<ld-football-scores-competition-predicted-lineups live='false' model='")[1].Split('\'')[0];
            var values = json.FromJson<Rootobject>();
            foreach (var match in values.matchList)
            {
                foreach (var player in match.home.playerList.startingLineup)
                {
                    var chancedPlayer = new ChancedRealPlayer
                    {
                        Name = player.fullname,
                        RealTeam = new RealTeam { Name = match.home.name },
                        Chance = new Chance()
                        {
                            Sky = true
                        }
                    };
                    players.Add(chancedPlayer);
                }
                foreach (var player in match.away.playerList.startingLineup)
                {
                    var chancedPlayer = new ChancedRealPlayer
                    {
                        Name = player.fullname,
                        RealTeam = new RealTeam { Name = match.away.name },
                        Chance = new Chance()
                        {
                            Sky = true
                        }
                    };
                    players.Add(chancedPlayer);
                }
            }
            return players;
        }

        public class Rootobject
        {
            public DateTime createdAt { get; set; }
            public string competitionId { get; set; }
            public int round { get; set; }
            public Matchlist[] matchList { get; set; }
            public string __typename { get; set; }
            public DateTime updatedAt { get; set; }
        }

        public class Matchlist
        {
            public DateTime date { get; set; }
            public Away away { get; set; }
            public string lmpLink { get; set; }
            public string matchId { get; set; }
            public Home home { get; set; }
            public string status { get; set; }
            public string ctaUrlAway { get; set; }
            public string ctaUrlHome { get; set; }
            public string ctaLmpUrl { get; set; }
        }

        public class Away
        {
            public string ctaUrl { get; set; }
            public string formation { get; set; }
            public string id { get; set; }
            public string logoUrl { get; set; }
            public string name { get; set; }
            public string seoName { get; set; }
            public string shortname { get; set; }
            public Playerlist playerList { get; set; }
        }

        public class Playerlist
        {
            public Disqualified[] disqualifieds { get; set; }
            public Manager manager { get; set; }
            public Potentialplayer[] potentialPlayers { get; set; }
            public Substitute[] substitutes { get; set; }
            public Unavailable[] unavailables { get; set; }
            public Startinglineup[] startingLineup { get; set; }
        }

        public class Manager
        {
            public object id { get; set; }
            public object role { get; set; }
            public string fullname { get; set; }
        }

        public class Disqualified
        {
            public object id { get; set; }
            public object role { get; set; }
            public string fullname { get; set; }
        }

        public class Potentialplayer
        {
            public object id { get; set; }
            public object role { get; set; }
            public string fullname { get; set; }
        }

        public class Substitute
        {
            public object id { get; set; }
            public object role { get; set; }
            public string fullname { get; set; }
        }

        public class Unavailable
        {
            public object id { get; set; }
            public object role { get; set; }
            public string fullname { get; set; }
        }

        public class Startinglineup
        {
            public int jerseyNum { get; set; }
            public string photoUrl { get; set; }
            public string role { get; set; }
            public string surname { get; set; }
            public string formationPlace { get; set; }
            public string name { get; set; }
            public string fullName { get; set; }
            public string id { get; set; }
            public string fullname { get; set; }
        }

        public class Home
        {
            public string ctaUrl { get; set; }
            public string formation { get; set; }
            public string id { get; set; }
            public string logoUrl { get; set; }
            public string name { get; set; }
            public string seoName { get; set; }
            public string shortname { get; set; }
            public Playerlist1 playerList { get; set; }
        }

        public class Playerlist1
        {
            public Disqualified1[] disqualifieds { get; set; }
            public Manager1 manager { get; set; }
            public Potentialplayer1[] potentialPlayers { get; set; }
            public Substitute1[] substitutes { get; set; }
            public Unavailable1[] unavailables { get; set; }
            public Startinglineup1[] startingLineup { get; set; }
        }

        public class Manager1
        {
            public object id { get; set; }
            public object role { get; set; }
            public string fullname { get; set; }
        }

        public class Disqualified1
        {
            public object id { get; set; }
            public object role { get; set; }
            public string fullname { get; set; }
        }

        public class Potentialplayer1
        {
            public object id { get; set; }
            public object role { get; set; }
            public string fullname { get; set; }
        }

        public class Substitute1
        {
            public object id { get; set; }
            public object role { get; set; }
            public string fullname { get; set; }
        }

        public class Unavailable1
        {
            public object id { get; set; }
            public object role { get; set; }
            public string fullname { get; set; }
        }

        public class Startinglineup1
        {
            public int jerseyNum { get; set; }
            public string photoUrl { get; set; }
            public string role { get; set; }
            public string surname { get; set; }
            public string formationPlace { get; set; }
            public string name { get; set; }
            public string fullName { get; set; }
            public string id { get; set; }
            public string fullname { get; set; }
        }

    }
}
