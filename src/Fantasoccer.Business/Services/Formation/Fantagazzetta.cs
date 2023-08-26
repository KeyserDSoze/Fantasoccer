using System.Text.RegularExpressions;
using System.Web;

namespace Fantasoccer.Engine.IngestionData
{
    internal class Fantagazzetta : IFormationParser
    {
        private static readonly Regex PlayerListRegex = new("<ul class=\"player-list starters\">");
        private static readonly Regex PlayerItemRegex = new("<li class=\"player-item");
        private static readonly Regex SpanRegex = new(@"<span>[^<]*</span>");
        public async Task<List<ChancedRealPlayer>> GetPlayersAsync(HttpClient httpClient)
        {
            List<ChancedRealPlayer> players = new();
            var returnedValue = await httpClient.GetStringAsync("https://www.fantacalcio.it/probabili-formazioni-serie-a").NoContext();
            var matches = PlayerListRegex.Split(returnedValue);
            foreach (var match in matches.Skip(1))
            {
                foreach (var player in PlayerItemRegex.Split(match).Skip(1).Take(11))
                {
                    if (string.IsNullOrWhiteSpace(player))
                        continue;
                    var name = SpanRegex.IsMatch(player) ? HttpUtility.HtmlDecode(SpanRegex.Match(player).Value.Split('>')[1].Split('<')[0]) : string.Empty;
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    players.Add(new()
                    {
                        Name = KeyUtils.GetPlayerKey(name),
                        Year = FantacalcioTime.ActualYear,
                        Chance = new Chance()
                        {
                            Fantagazzetta = true
                        }
                    });
                }
            }
            return players;
        }
    }
}
