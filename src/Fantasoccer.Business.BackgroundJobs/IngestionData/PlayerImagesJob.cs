using System.Net;
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
        private static readonly Regex s_link = new("class=\"spieler-zelle\"");
        private static readonly Regex s_linkTeam = new("class=\"club\"");
        private static readonly Regex s_image = new("https://img.a.transfermarkt.technology");
        public PlayerImagesJob(IFantasoccerLogger logger, IHttpClientFactory httpClientFactory,
            IRepository<RealPlayersWrapper, int> realPlayers,
            IFactory<IContentRepository> factory)
        {
            _httpClient = httpClientFactory.CreateClient("base");
            _logger = logger;
            _realPlayers = realPlayers;
            _contentRepository = factory.Create("images");
        }

        public Task ActionToDoAsync()
            => InvokeAsync();

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<PlayerImagesJob>(exception);

        private async Task InvokeAsync()
        {
            var year = FantacalcioTime.ActualYear;
            var players = (await _realPlayers.GetAsync(year).NoContext())?.Players;
            if (players != null)
            {
                foreach (var player in players)
                {
                    var url = string.Format("https://www.transfermarkt.it/schnellsuche/ergebnis/schnellsuche?query={0}&x=0&y=0", player.Name);
                    var playerLandingPage = await _httpClient.GetStringAsync(url).NoContext();
                    var linkPlayer = string.Empty;
                    foreach (var match in s_link.Split(playerLandingPage).Skip(1))
                    {
                        var teamName = s_linkTeam.Split(match)[1].Split('"')[0].Split('>')[1].Split('<')[0];
                        if (teamName.ToLower().Contains(player.RealTeam.ToLower()))
                        {
                            linkPlayer = match.Split('"')[1];
                            break;
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(linkPlayer))
                    {
                        var laUrlDelGiocatore = $"https://www.transfermarkt.it{linkPlayer}";
                        playerLandingPage = await _httpClient.GetStringAsync(laUrlDelGiocatore).NoContext();
                        if (s_image.IsMatch(playerLandingPage))
                        {
                            var imageAsUri = s_image.Split(playerLandingPage)[1];
                            var downloadUrl = $"{s_image}{imageAsUri.Split('"')[0]}";
                            using (WebClient client = new())
                            {
                                var image = client.DownloadData(downloadUrl);
                                await _contentRepository.UploadAsync($"players/{player.Key}.jpg",
                                    image, new ContentRepositoryOptions
                                    {
                                        HttpHeaders = new ContentRepositoryHttpHeaders
                                        {
                                            ContentType = "image/jpg"
                                        }
                                    }, true).NoContext();
                            }
                        }
                    }
                    await Task.Delay(30);
                }
            }
        }
    }
}
