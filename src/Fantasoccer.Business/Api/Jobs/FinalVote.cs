using System.Text.RegularExpressions;
using System.Web;
using RepositoryFramework;

namespace Fantasoccer.Business
{
    internal class FinalVote
    {
        private static readonly Regex s_trRegex = new("<div class=\"player-item");
        private static readonly Regex s_tbodyRegex = new("<tbody>");
        private static readonly Regex s_spanRegex = new(@"<span>[^<]*</span>");
        private static readonly Regex s_takeTheVote = new("data-value=");
        private static readonly Regex s_takeTheTeams = new("<a class=\"team-name");
        private static readonly Regex s_takeTheBody = new("<ul class=\"teams");
        private static readonly Regex s_nameOfTeamRegex = new("content=");
        private static readonly Regex s_forRole = new("data-value=");
        private static readonly Regex s_tdedRegex = new("</td>");
        private readonly HttpClient _httpClient;
        private readonly IRepository<RealCalendar, int> _realCalendarRepository;
        private readonly IRepository<RealPlayersWrapper, int> _realPlayerRepository;

        public FinalVote(IHttpClientFactory httpClientFactory,
            IRepository<RealCalendar, int> realCalendarRepository,
            IRepository<RealPlayersWrapper, int> realPlayerRepository,
            IRepository<vot>)
        {
            _httpClient = httpClientFactory.CreateClient("base");
            _realCalendarRepository = realCalendarRepository;
            _realPlayerRepository = realPlayerRepository;
        }
        public async Task<List<Player>> SaveFinalVoteAsync(int year, int serieADay, bool forceRefresh)
        {
            var fantacalcioYear = FantacalcioTime.LabelYear(year).Replace("/20", "-");
            var value = await _httpClient.GetStringAsync($"https://www.fantacalcio.it/voti-fantacalcio-serie-a/{fantacalcioYear}/{serieADay}").NoContext();
            List<Player> players = new();
            if (!string.IsNullOrWhiteSpace(value))
            {
                var values = s_takeTheTeams.Split(s_takeTheBody.Split(value)[1]);
                foreach (var squadra in values.Skip(1).Take(20))
                {
                    if (!s_trRegex.IsMatch(squadra))
                        continue;
                    var realTeam = s_nameOfTeamRegex.Split(squadra)[1].Split('"')[1].ToLower();
                    realTeam = realTeam.Substring(0, 1).ToUpper() + realTeam.Substring(1).ToLower();
                    foreach (var element in s_trRegex.Split(s_tbodyRegex.Split(squadra)[1]).Skip(1))
                    {

                        var elementSplitted = s_tdedRegex.Split(element);
                        Player player = new(FantacalcioId.GetPlayerKey(year, GetName(element)), 0, default);
                        var vote = GetVote(elementSplitted[1]);
                        var role = GetRole(element).ToLower() switch
                        {
                            "a" => Role.Forward,
                            "c" => Role.Midfielder,
                            "d" => Role.Defensor,
                            "p" => Role.GoalKeeper,
                            _ => Role.Undefined
                        };
                        var anotherSplitting = s_takeTheVote.Split(elementSplitted[2]);
                        player.Vote = new Vote(role,
                            vote < 0 ? 0 : vote,
                            true,
                            GetSomethingDone(anotherSplitting[1]),
                            GetSomethingDone(anotherSplitting[4]),
                            GetSomethingDone(anotherSplitting[7]),
                            GetSomethingDone(anotherSplitting[6]),
                            GetSomethingDone(anotherSplitting[2]),
                            GetSomethingDone(anotherSplitting[5]),
                            GetSomethingDone(anotherSplitting[3]),
                            element.Contains("red-card") ? Behaviour.RedCard :
                                (element.Contains("yellow-card") ? Behaviour.YellowCard : Behaviour.Nothing),
                            vote != -1 && HasVote(elementSplitted[1]), false, true);
                        players.Add(player);

                    }
                }
            }
            var serieACalendar = await _realCalendarRepository.GetAsync(FantacalcioTime.ActualYear).NoContext();
            var day = serieACalendar!.Days.FirstOrDefault(x => x.SerieADay == serieADay);
            var allPlayers = await _realPlayerRepository.GetAsync(FantacalcioTime.ActualYear).NoContext();
            foreach (RealGame game in day.Games.Where(x => x.Delayed))
            {
                Delayed(game.AbbreviatedHome);
                Delayed(game.AbbreviatedAway);
            }
            void Delayed(string realTeamName)
            {
                foreach (var player in allPlayers.Players.Where(x => x.RealTeam == realTeamName))
                {
                    players.Add(new(player.Key, 0, default)
                    {
                        Vote = new Vote(player.Role, 6, true, 0, 0, 0, 0, 0, 0, 0, Behaviour.Nothing, true, false, false)
                    });
                }
            }
            await new FinalVotesContext(year, serieADay).SaveAsync(players, forceRefresh).NoContext();
            return players;
        }
        private static string GetName(string name) => s_spanRegex.IsMatch(name) ? HttpUtility.HtmlDecode(s_spanRegex.Match(name).Value.Split('>')[1].Split('<')[0]) : string.Empty;
        private static string GetRole(string role) => HttpUtility.HtmlDecode((s_forRole.Split(role)[1]).Split('"')[1]);
        private static bool HasVote(string check) => !check.ToLower().Contains("55");
        private static float GetVote(string voto)
        {
            try
            {
                voto = s_takeTheVote.Split(voto)[1].Split('"')[1];
                var finalVote = float.Parse(voto, new System.Globalization.CultureInfo("it"));
                if (finalVote == 55f)
                    finalVote = -1;
                return finalVote;
            }
            catch
            {
                return -1;
            }
        }
        private static int GetSomethingDone(string value)
        {
            value = value.Split('"')[1].Trim();
            if (string.IsNullOrWhiteSpace(value))
                return 0;
            if (value.Equals("-"))
                return 0;
            else
            {
                try
                {
                    return int.Parse(value);
                }
                catch
                {
                    return 0;
                }
            }
        }
        private static bool GetYellowCard(string check) => check.ToLower().Contains("trn-ry");
        private static bool GetRedCard(string check) => check.ToLower().Contains("trn-rr");
    }
}
