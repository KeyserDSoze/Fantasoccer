using FantacalcioV4.Database;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace FantacalcioV4.Engine.IngestionData.FormationParser
{
    internal class Mediaset : IFormationParser
    {
        public async Task<List<Player>> GetPlayersAsync(HttpClient httpClient)
        {
            List<Player> players = new();
            var teams = await AllTeamKey.Active.InstanceAsync().NoContext();
            foreach (var team in teams)
            {
                try
                {
                    var returnedValue = await httpClient.
                        GetFromJsonAsync<Rootobject>($"https://www.sportmediaset.mediaset.it/calcio/squadre/squadra-{team.PrimaryKey.ToLower()}.json")
                            .NoContext();
                    foreach (var player in returnedValue.titolari.content.Tables[0].Rows)
                    {
                        Player chancedPlayer = new(FantacalcioId.GetPlayerKey(FantacalcioTime.ActualYear, player.giocatore.ToLower()), 0, default)
                        {
                            Chance = new Chance(false, false, true, false, ChanceType.Normal)
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