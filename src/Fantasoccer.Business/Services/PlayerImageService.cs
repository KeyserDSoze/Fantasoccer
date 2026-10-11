using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;
using Rystem.Content;

namespace Fantasoccer.Business.Services;

// Both the scheduled job and manual refresh use the same official source and blob key.
public sealed class PlayerImageService(
    IHttpClientFactory httpClientFactory,
    IRepository<RealPlayersWrapper, int> realPlayers,
    IFactory<IContentRepository> contentFactory)
{
    private const string CompetitionId = "serie-a::Football_Competition::ec93b94f74294dc98ab5bcfd67fc0d88";
    private static readonly Uri MediaBaseUri = new("https://media-sdp.legaseriea.it/");
    private const int MaximumImageBytes = 10 * 1024 * 1024;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("player-images");
    private readonly IContentRepository _images = contentFactory.Create("images")
        ?? throw new InvalidOperationException("Images repository is not configured.");

    public async Task DownloadMissingAsync(CancellationToken cancellationToken)
    {
        var year = FantacalcioTime.ActualYear;
        var players = (await realPlayers.GetAsync(year, cancellationToken))?.Players;
        if (players == null || players.Count == 0) return;
        var sourcePlayers = await GetSourcePlayersAsync(year, cancellationToken);
        foreach (var player in players)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _images.ExistAsync(ImageKey(player), cancellationToken)) continue;
            var path = FindImagePath(player, sourcePlayers);
            if (path == null) continue;
            try
            {
                await UploadAsync(player, path, cancellationToken);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                continue;
            }
            await Task.Delay(30, cancellationToken);
        }
    }

    public async Task<bool> ReloadAsync(RealPlayer player, CancellationToken cancellationToken)
    {
        // Always reread the current catalog, even when the blob already exists.
        var sourcePlayers = await GetSourcePlayersAsync(FantacalcioTime.ActualYear, cancellationToken);
        var path = FindImagePath(player, sourcePlayers);
        if (path == null) return false;
        await UploadAsync(player, path, cancellationToken);
        return true;
    }

    private static string ImageKey(RealPlayer player) => $"players/{player.Key}.jpg";

    private async Task UploadAsync(RealPlayer player, string path, CancellationToken cancellationToken)
    {
        var uri = new Uri(MediaBaseUri, path);
        if (uri.Scheme != Uri.UriSchemeHttps || uri.Host != MediaBaseUri.Host || !uri.IsDefaultPort || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException("Invalid official image URL.");
        using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var contentType = response.Content.Headers.ContentType?.MediaType;
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp"))
            throw new InvalidDataException("The source did not return a supported image.");
        if (response.Content.Headers.ContentLength > MaximumImageBytes)
            throw new InvalidDataException("The image is too large.");
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var image = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (image.Length + read > MaximumImageBytes)
                throw new InvalidDataException("The image is too large.");
            image.Write(buffer, 0, read);
        }
        if (image.Length == 0) throw new InvalidDataException("The image is empty.");
        await _images.UploadAsync(ImageKey(player), image.ToArray(), new ContentRepositoryOptions
        {
            HttpHeaders = new ContentRepositoryHttpHeaders { ContentType = contentType, CacheControl = "public, max-age=300" }
        }, true, cancellationToken);
    }

    private async Task<List<SdpPlayer>> GetSourcePlayersAsync(int year, CancellationToken cancellationToken)
    {
        var seasons = await _httpClient.GetFromJsonAsync<SeasonsResponse>(
            $"competitions/{CompetitionId}/seasons?locale=it-IT", cancellationToken);
        var currentSeason = seasons?.Seasons?.FirstOrDefault(x => x.SeasonName == FantacalcioTime.LabelYear(year));
        if (currentSeason == null) return [];
        var players = await GetPlayersAsync(currentSeason.SeasonId, cancellationToken);
        var previousSeason = seasons?.Seasons?.FirstOrDefault(x => x.SeasonName == FantacalcioTime.LabelYear(year - 1));
        if (previousSeason != null)
            players.AddRange(await GetPlayersAsync(previousSeason.SeasonId, cancellationToken));
        return players;
    }

    private async Task<List<SdpPlayer>> GetPlayersAsync(string seasonId, CancellationToken cancellationToken)
    {
        List<SdpPlayer> players = [];
        for (var page = 1; page <= 100; page++)
        {
            var response = await _httpClient.GetFromJsonAsync<PlayersResponse>(
                $"seasons/{Uri.EscapeDataString(seasonId)}/stats/players?category=General&page={page}&locale=it-IT", cancellationToken);
            if (response?.Players != null) players.AddRange(response.Players);
            if (response?.Pagination == null || response.Pagination.IsLastPage || page >= response.Pagination.TotalPages) break;
        }
        return players;
    }

    private static string? FindImagePath(RealPlayer player, IEnumerable<SdpPlayer> sourcePlayers)
    {
        var name = NormalizeName(player.Name);
        var team = NormalizeName(player.RealTeam?.Name);
        var role = player.Role switch
        {
            Role.GoalKeeper => 1, Role.Defensor => 2, Role.Midfielder => 3, Role.Forward => 4, _ => 0
        };
        var candidates = sourcePlayers
            .Where(x => Names(x).Any(value => NormalizeName(value) == name) && (role == 0 || x.Role == role))
            .Select(x => new { Player = x, Path = GetImagePath(x) })
            .Where(x => x.Path != null)
            .ToList();
        return candidates.FirstOrDefault(x => string.IsNullOrWhiteSpace(team) || TeamNames(x.Player).Any(value => NormalizeName(value) == team))?.Path
            ?? candidates.FirstOrDefault()?.Path;
    }

    private static IEnumerable<string?> Names(SdpPlayer player)
        => [player.ShortName, player.DisplayName, $"{player.MediaFirstName} {player.MediaLastName}"];

    private static IEnumerable<string?> TeamNames(SdpPlayer player)
        => [player.Team?.ShortName, player.Team?.OfficialName, player.Team?.AcronymName];

    private static string? GetImagePath(SdpPlayer player)
        => player.Imagery?.FirstOrDefault(x => x.Key.EndsWith("_middle", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Value)).Value
            ?? player.Imagery?.FirstOrDefault(x => x.Key.EndsWith("_celeb", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(x.Value)).Value
            ?? player.Imagery?.Values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));

    private static string NormalizeName(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var normalized = WebUtility.HtmlDecode(input).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        var value = Regex.Replace(builder.ToString(), @"['’`´]", string.Empty);
        return Regex.Replace(value, @"\s+", " ").ToLowerInvariant().Trim();
    }

    private sealed class SeasonsResponse { public List<Season>? Seasons { get; set; } }
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
