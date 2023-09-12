using Rystem;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace FantacalcioV4.Database
{
    public static class DownloadPlayerImages
    {
        private static readonly Regex Link = new("class=\"spieler-zelle\"");
        private static readonly Regex LinkTeam = new("class=\"club\"");
        private static readonly Regex Image = new("https://img.a.transfermarkt.technology");
        public static async Task InvokeAsync(int year, HttpClient httpClient)
        {
            var players = await new AllPlayerKey() { Year = year }.InstanceAsync();
            foreach (var player in players)
            {
                string url = string.Format("https://www.transfermarkt.it/schnellsuche/ergebnis/schnellsuche?query={0}&x=0&y=0", player.Name);
                string playerLandingPage = await httpClient.GetStringAsync(url);
                string linkPlayer = string.Empty;
                foreach (var match in Link.Split(playerLandingPage).Skip(1))
                {
                    string teamName = LinkTeam.Split(match)[1].Split('"')[0].Split('>')[1].Split('<')[0];
                    if (teamName.ToLower().Contains(player.RealTeam.ToLower()))
                    {
                        linkPlayer = match.Split('"')[1];
                        break;
                    }
                }

                
                await Task.Delay(30);
            }
        }
    }
}
