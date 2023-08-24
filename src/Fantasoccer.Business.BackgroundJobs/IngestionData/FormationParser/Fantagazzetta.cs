using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace FantacalcioV4.Engine.IngestionData.FormationParser
{
    internal class Fantagazzetta : IFormationParser
    {
        private static readonly Regex PlayerListRegex = new("<ul class=\"player-list starters\">");
        private static readonly Regex PlayerItemRegex = new("<li class=\"player-item");
        private static readonly Regex SpanRegex = new(@"<span>[^<]*</span>");
        public async Task<List<Player>> GetPlayersAsync(HttpClient httpClient)
        {
            List<Player> players = new();
            string returnedValue = await httpClient.GetStringAsync("https://www.fantacalcio.it/probabili-formazioni-serie-a").NoContext();
            string[] matches = PlayerListRegex.Split(returnedValue);
            foreach (var match in matches.Skip(1))
            {
                foreach (var player in PlayerItemRegex.Split(match).Skip(1).Take(11))
                {
                    if (string.IsNullOrWhiteSpace(player))
                        continue;
                    string name = SpanRegex.IsMatch(player) ? HttpUtility.HtmlDecode(SpanRegex.Match(player).Value.Split('>')[1].Split('<')[0]) : string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    players.Add(new(FantacalcioId.GetPlayerKey(FantacalcioTime.ActualYear, name), 0, default)
                    {
                        Chance = new Chance(true, false, false, false, ChanceType.Normal)
                    });
                }
            }
            return players;
        }
    }
}