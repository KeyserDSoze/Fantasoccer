using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Timers;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Rystem.Content;

namespace Fantasoccer.Engine.IngestionData
{
    public class PlayerImagesJob : IBackgroundJob
    {
        private const string CompetitionId = "serie-a::Football_Competition::ec93b94f74294dc98ab5bcfd67fc0d88";
        private static readonly Uri s_mediaBaseUri = new("https://media-sdp.legaseriea.it/");
        private readonly HttpClient _httpClient;
        private readonly IFantasoccerLogger _logger;
        private readonly IRepository<RealPlayersWrapper, int> _realPlayers;
        private readonly IContentRepository _contentRepository;

        public PlayerImagesJob(
            IFantasoccerLogger logger,
            IHttpClientFactory httpClientFactory,
            IRepository<RealPlayersWrapper, int> realPlayers,
            IFactory<IContentRepository> factory)
        {
            _httpClient = httpClientFactory.CreateClient("player-images");
            _logger = logger;
            _realPlayers = realPlayers;
            _contentRepository = factory.Create("images") ?? throw new InvalidOperationException("Images repository is not configured.");
        }

        public Task ActionToDoAsync(CancellationToken cancellationToken)
            => InvokeAsync(cancellationToken);

        public Task OnException(Exception exception)
            => _logger.LogErrorAsync<PlayerImagesJob>(exception);

        private async Task InvokeAsync(CancellationToken cancellationToken)
        {
            var year = FantacalcioTime.ActualYear;
            var players = (await _realPlayers.GetAsync(year, cancellationToken).NoContext())?.Players;
            if (players == null || players.Count == 0)
                return;

            List<SdpPlayer> imagePlayers;
            try
            {
                var seasons = await _httpClient.GetFromJsonAsync<SeasonsResponse>(
                    $"competitions/{CompetitionId}/seasons?locale=it-IT",
                    cancellationToken).NoContext();
                var currentSeason = seasons?.Seasons?.FirstOrDefault(x => x.SeasonName == FantacalcioTime.LabelYear(year));
                if (currentSeason == null)
                    return;

                imagePlayers = await GetPlayersAsync(currentSeason.SeasonId, cancellationToken).NoContext();
                var previousSeason = seasons?.Seasons?.FirstOrDefault(x => x.SeasonName == FantacalcioTime.LabelYear(year - 1));
                if (previousSeason != null)
                    imagePlayers.AddRange(await GetPlayersAsync(previousSeason.SeasonId, cancellationToken).NoContext());
            }
            catch (HttpRequestException)
            {
                return;
            }
            catch (JsonException)
            {
                return;
            }

            foreach (var player in players)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var imagePath = FindImagePath(player, imagePlayers);
                if (imagePath == null)
                    continue;

                var imageName = $"players/{player.Key}.jpg";
                if (await _contentRepository.ExistAsync(imageName, cancellationToken))
                    continue;

                try
                {
                    using var response = await _httpClient.GetAsync(new Uri(s_mediaBaseUri, imagePath), cancellationToken).NoContext();
                    response.EnsureSuccessStatusCode();
                    var image = await response.Content.ReadAsByteArrayAsync(cancellationToken).NoContext();
                    await _contentRepository.UploadAsync(imageName, image, new ContentRepositoryOptions
                    {
                        HttpHeaders = new ContentRepositoryHttpHeaders
                        {
                            ContentType = response.Content.Headers.ContentType?.MediaType ?? "image/webp"
                        }
                    }, true, cancellationToken).NoContext();
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    continue;
                }

                await Task.Delay(30, cancellationToken).NoContext();
            }
        }

        private async Task<List<SdpPlayer>> GetPlayersAsync(string seasonId, CancellationToken cancellationToken)
        {
            List<SdpPlayer> players = [];
            for (var page = 1; page <= 100; page++)
            {
                var response = await _httpClient.GetFromJsonAsync<PlayersResponse>(
                    $"seasons/{Uri.EscapeDataString(seasonId)}/stats/players?category=General&page={page}&locale=it-IT",
                    cancellationToken).NoContext();
                if (response?.Players != null)
                    players.AddRange(response.Players);
                if (response?.Pagination == null || response.Pagination.IsLastPage || page >= response.Pagination.TotalPages)
                    break;
            }
            return players;
        }

        private static string? FindImagePath(RealPlayer player, IEnumerable<SdpPlayer> imagePlayers)
        {
            var normalizedName = NormalizeName(player.Name);
            var normalizedTeam = NormalizeName(player.RealTeam?.Name);
            var role = player.Role switch
            {
                Role.GoalKeeper => 1,
                Role.Defensor => 2,
                Role.Midfielder => 3,
                Role.Forward => 4,
                _ => 0
            };
            var candidates = imagePlayers
                .Select(x => new { Player = x, ImagePath = GetImagePath(x) })
                .Where(x => x.ImagePath != null)
                .ToList();
            var exact = candidates.FirstOrDefault(x =>
                Names(x.Player).Any(name => NormalizeName(name) == normalizedName) &&
                (role == 0 || x.Player.Role == role) &&
                (string.IsNullOrWhiteSpace(normalizedTeam) || TeamNames(x.Player).Any(team => NormalizeName(team) == normalizedTeam)));
            exact ??= candidates.FirstOrDefault(x =>
                Names(x.Player).Any(name => NormalizeName(name) == normalizedName) &&
                (role == 0 || x.Player.Role == role));
            return exact?.ImagePath;
        }

        private static IEnumerable<string?> Names(SdpPlayer player)
        {
            yield return player.ShortName;
            yield return player.DisplayName;
            yield return $"{player.MediaFirstName} {player.MediaLastName}";
        }

        private static IEnumerable<string?> TeamNames(SdpPlayer player)
        {
            yield return player.Team?.ShortName;
            yield return player.Team?.OfficialName;
            yield return player.Team?.AcronymName;
        }

        private static string? GetImagePath(SdpPlayer player)
        {
            if (player.Imagery == null)
                return null;
            return player.Imagery.FirstOrDefault(x => x.Key.EndsWith("_middle", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Value)).Value
                ?? player.Imagery.FirstOrDefault(x => x.Key.EndsWith("_celeb", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Value)).Value
                ?? player.Imagery.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
        }

        private static string NormalizeName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var normalized = WebUtility.HtmlDecode(input).Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var character in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                    builder.Append(character);
            }
            var value = Regex.Replace(builder.ToString(), @"['’`´]", string.Empty);
            return Regex.Replace(value, @"\s+", " ").ToLowerInvariant().Trim();
        }

        private sealed class SeasonsResponse
        {
            public List<Season>? Seasons { get; set; }
        }

        private sealed class Season
        {
            public string SeasonId { get; set; } = string.Empty;
            public string SeasonName { get; set; } = string.Empty;
        }

        private sealed class PlayersResponse
        {
            public Pagination? Pagination { get; set; }
            public List<SdpPlayer>? Players { get; set; }
        }

        private sealed class Pagination
        {
            public int TotalPages { get; set; }
            public bool IsLastPage { get; set; }
        }

        private sealed class SdpPlayer
        {
            public int Role { get; set; }
            public string? MediaFirstName { get; set; }
            public string? MediaLastName { get; set; }
            public string? ShortName { get; set; }
            public string? DisplayName { get; set; }
            public SdpTeam? Team { get; set; }
            public Dictionary<string, string?>? Imagery { get; set; }
        }

        private sealed class SdpTeam
        {
            public string? ShortName { get; set; }
            public string? OfficialName { get; set; }
            public string? AcronymName { get; set; }
        }
    }
}
