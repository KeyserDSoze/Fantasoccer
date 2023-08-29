using System.Net.Http.Json;
using RepositoryFramework;

namespace Fantasoccer.Business.IngestionData
{
    internal class Mediaset : IFormationParser
    {
        private readonly IRepository<RealCalendar, int> _repository;

        private readonly HttpClient _httpClient;
        public Mediaset(IHttpClientFactory httpClientFactory, IRepository<RealCalendar, int> repository)
        {
            _httpClient = httpClientFactory.CreateClient("base");
            _repository = repository;
        }
        public async Task<List<ChancedRealPlayer>> GetPlayersAsync()
        {
            List<ChancedRealPlayer> players = new();
            var teams = (await _repository.GetAsync(FantacalcioTime.ActualYear).NoContext()).Days.First().Games.SelectMany(x => new string[2] { x.Home, x.Away }).ToList();
            foreach (var team in teams)
            {
                var realTeam = team == "Juve" ? "Juventus" : team;
                try
                {
                    var returnedValue = await _httpClient.
                        GetFromJsonAsync<Rootobject>($"https://www.sportmediaset.mediaset.it/calcio/squadre/squadra-{realTeam.ToLower()}.json")
                            .NoContext();
                    foreach (var player in returnedValue.titolari.content.Tables[0].Rows)
                    {
                        var chancedPlayer = new ChancedRealPlayer
                        {
                            Name = KeyUtils.GetPlayerKey(player.giocatore.ToLower()),
                            Year = FantacalcioTime.ActualYear,
                            RealTeam = realTeam,
                            Chance = new Chance()
                            {
                                Gazzetta = true
                            }
                        };
                        players.Add(chancedPlayer);
                    }
                }
                catch (Exception ex)
                {
                    string sasd = ex.Message;
                }
            }
            return players;
        }
        private class Rootobject
        {
            public Titolari titolari { get; set; }
            public Sostituzioni sostituzioni { get; set; }
            public string squalificati { get; set; }
            public string indisponibiliformazione { get; set; }
            public string modulo { get; set; }
            public string ufficiali { get; set; }
            public string allenatore { get; set; }
            public string kpm3id { get; set; }
            public string teamOptaId { get; set; }
            public string urlSquadra { get; set; }
        }
        private class Titolari
        {
            public string title { get; set; }
            public Content content { get; set; }
        }

        private class Content
        {
            public Table[] Tables { get; set; }
        }

        private class Table
        {
            public string id { get; set; }
            public object[] didascalie { get; set; }
            public Row[] Rows { get; set; }
        }
        private class Row
        {
            public string numeromaglia { get; set; }
            public string giocatore { get; set; }
        }
        private class Sostituzioni
        {
            public string title { get; set; }
            public Content1 content { get; set; }
        }
        private class Content1
        {
            public Table1[] Tables { get; set; }
        }

        private class Table1
        {
            public string id { get; set; }
            public object[] didascalie { get; set; }
            public Row1[] Rows { get; set; }
        }

        private class Row1
        {
            public string numeromaglia { get; set; }
            public string giocatore { get; set; }
        }

    }
}
