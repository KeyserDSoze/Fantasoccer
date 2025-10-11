using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Rystem.Content;

namespace Fantasoccer.Engine.IngestionData
{
    public class PlayerImagesJob : IBackgroundJob
    {
        private readonly HttpClient _httpClient;
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<RealPlayersWrapper, int> _realPlayers;
        private readonly IContentRepository _contentRepository;
        public PlayerImagesJob(IFantasoccerLogger logger, IHttpClientFactory httpClientFactory,
            IRepository<RealPlayersWrapper, int> realPlayers,
            IFactory<IContentRepository> factory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
            _logger = logger;
            _realPlayers = realPlayers;
            _contentRepository = factory.Create("images");
        }

        public Task ActionToDoAsync(CancellationToken cancellationToken)
            => InvokeAsync();

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<PlayerImagesJob>(exception);
        private static bool IsThisPlayer(Body player, string surname, string team, string role)
        {
            var playerSurname = NormalizeName(player.surname).ToLower();
            var playerName = NormalizeName(player.name).ToLower();
            var teamShortName = player.team_short_name.ToLower();
            var codeRole = player.cod_role.ToLower();
            if (playerSurname.Contains(surname) || surname.Contains(playerSurname) || playerName.Contains(surname) || surname.Contains(playerName))
            {
                if (teamShortName == team)
                {
                    return codeRole == role ||
                        (surname == playerSurname &&
                            ((codeRole == "a" && role == "c")
                                || (codeRole == "c" && role == "a")
                                || (codeRole == "d" && role == "c")
                                || (codeRole == "c" && role == "d")));
                }
            }
            return false;
        }
        private static string NormalizeName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            // 1️⃣ Decodifica eventuali entità HTML (es: &#39; -> ')
            var decoded = WebUtility.HtmlDecode(input);

            // 2️⃣ Normalizza in forma decomposed (NFD) per separare lettere e accenti
            var normalized = decoded.Normalize(NormalizationForm.FormD);

            // 3️⃣ Rimuove i segni diacritici (accenti)
            var sb = new StringBuilder();
            foreach (var c in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(c);
                }
            }

            string result = sb.ToString();

            // 4️⃣ Rimuove apostrofi, virgolette e altri segni simili
            result = Regex.Replace(result, @"['’`´]", "");

            // 5️⃣ Normalizza spazi multipli
            result = Regex.Replace(result, @"\s+", " ").Trim();

            return result;
        }
        private async Task InvokeAsync()
        {
            var year = FantacalcioTime.ActualYear;
            var players = (await _realPlayers.GetAsync(year).NoContext())?.Players;
            if (players != null)
            {
                var url = "https://www.legaseriea.it/api/widget/player?lang=it&offset=0&limit=1030";
                var playerImages = await _httpClient.GetFromJsonAsync<Rootobject>(url).NoContext();
                if (playerImages != null)
                    foreach (var player in players)
                    {
                        var imageAsUri = string.Empty;
                        var surname = NormalizeName(player.Name.Split(' ').OrderByDescending(x => x.Length).First().Split('.').OrderByDescending(x => x.Length).First().ToLower());
                        if (!surname.StartsWith("bernab"))
                            continue;
                        var teamShortName = player.RealTeam.Abbreviation.ToLower();
                        var possiblePlayer = playerImages.data.body.FirstOrDefault(x => IsThisPlayer(x, surname, teamShortName, player.Role.AsString().First().ToString().ToLower()));
                        if (possiblePlayer != null)
                        {
                            imageAsUri = possiblePlayer.medium_shot;
                        }
                        if (!string.IsNullOrWhiteSpace(imageAsUri))
                        {
                            await Try.WithDefaultOnCatchAsync(async () =>
                            {
                                var imageName = $"players/{player.Key}.jpg";
                                if (!await _contentRepository.ExistAsync(imageName))
                                {
                                    using (WebClient client = new())
                                    {
                                        var image = client.DownloadData(imageAsUri);
                                        await _contentRepository.UploadAsync(imageName,
                                            image, new ContentRepositoryOptions
                                            {
                                                HttpHeaders = new ContentRepositoryHttpHeaders
                                                {
                                                    ContentType = "image/png"
                                                }
                                            }, true).NoContext();
                                    }
                                }
                            });

                        }
                        await Task.Delay(30);
                    }
            }
        }


        public class Rootobject
        {
            public bool success { get; set; }
            public string message { get; set; }
            public object[] errors { get; set; }
            public Data data { get; set; }
            public int code { get; set; }
        }

        public class Data
        {
            public string name { get; set; }
            public string type { get; set; }
            public string uicode { get; set; }
            public Body[] body { get; set; }
            public Data1 data { get; set; }
        }

        public class Data1
        {
            public string _limit { get; set; }
            public int _total { get; set; }
        }

        public class Body
        {
            public string active { get; set; }
            public string birthday { get; set; }
            public string cod_role { get; set; }
            public string formation { get; set; }
            public string id { get; set; }
            public string image { get; set; }
            public string iso3 { get; set; }
            public string medium_shot { get; set; }
            public string name { get; set; }
            public string nationality { get; set; }
            public string netco_id { get; set; }
            public string number { get; set; }
            public string role { get; set; }
            public string short_name { get; set; }
            public string surname { get; set; }
            public string team_id { get; set; }
            public string team_logo { get; set; }
            public string team_short_name { get; set; }
            public string team_slug { get; set; }
            public string url { get; set; }
        }

    }
}
