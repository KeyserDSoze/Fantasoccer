using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Fantasoccer.Business.Services;

// Both the scheduled job and manual refresh use the same official source and blob key.
public sealed class PlayerImageService(
    IHttpClientFactory httpClientFactory,
    IFootballIngestionReader realPlayers,
    IPlayerImageStore images,
    ILock imageLock) : IPlayerImageRefresher
{
    private const string CompetitionId = "serie-a::Football_Competition::ec93b94f74294dc98ab5bcfd67fc0d88";
    private static readonly Uri MediaBaseUri = new("https://media-sdp.legaseriea.it/");
    private const int MaximumImageBytes = 10 * 1024 * 1024;
    private const int CataloguePageConcurrency = 4;
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("player-images");

    public async Task DownloadMissingAsync(CancellationToken cancellationToken)
    {
        var year = FantacalcioTime.ActualYear;
        var players = (await realPlayers.PlayersAsync(year).WaitAsync(cancellationToken))?.Players;
        if (players == null || players.Count == 0) return;
        var sourcePlayers = await GetSourcePlayersAsync(year, cancellationToken, players);
        foreach (var player in players)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await images.ExistsAsync(ImageKey(player), cancellationToken)) continue;
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
        var sourcePlayers = await GetSourcePlayersAsync(FantacalcioTime.ActualYear, cancellationToken, [player]);
        var path = FindImagePath(player, sourcePlayers);
        if (path == null) return false;
        await UploadAsync(player, path, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<PlayerImageReloadResult>> ReloadManyAsync(
        IReadOnlyCollection<RealPlayer> players,
        CancellationToken cancellationToken)
    {
        List<SdpPlayer> sourcePlayers;
        try
        {
            sourcePlayers = await GetSourcePlayersAsync(FantacalcioTime.ActualYear, cancellationToken, players);
        }
        catch (ExternalHttpDisabledException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return players.Select(player => new PlayerImageReloadResult(
                player.Key,
                player.Name,
                false,
                "Catalogo immagini non disponibile.")).ToArray();
        }

        List<PlayerImageReloadResult> results = [];
        foreach (var player in players.DistinctBy(x => x.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var completed = false;
            var succeeded = false;
            string? error = null;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));

            await imageLock.ExecuteAsync(async () =>
            {
                try
                {
                    var path = FindImagePath(player, sourcePlayers);
                    if (path == null)
                    {
                        error = "Nessuna foto ufficiale disponibile.";
                        return;
                    }

                    await UploadAsync(player, path, timeout.Token);
                    succeeded = true;
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    error = "Aggiornamento scaduto per timeout.";
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception)
                {
                    error = "Fonte immagini non disponibile o immagine non valida.";
                }
                finally
                {
                    completed = true;
                }
            }, $"player-image:{player.Key}");

            if (!completed) error = "Aggiornamento già in corso.";
            results.Add(new PlayerImageReloadResult(player.Key, player.Name, succeeded, error));
        }

        return results;
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
        if (contentType is not ("image/jpeg" or "image/png" or "image/webp" or "application/octet-stream" or "binary/octet-stream"))
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
        var imageData = image.ToArray();
        if (contentType is "application/octet-stream" or "binary/octet-stream")
        {
            contentType = DetectImageContentType(imageData);
            if (contentType == null)
                throw new InvalidDataException("The source returned an untyped file that is not a supported image.");
        }

        await images.UploadAsync(ImageKey(player), imageData, contentType, "public, max-age=300", cancellationToken);
    }

    private async Task<List<SdpPlayer>> GetSourcePlayersAsync(int year, CancellationToken cancellationToken, IReadOnlyCollection<RealPlayer> requested)
    {
        var seasons = await _httpClient.GetFromJsonAsync<SeasonsResponse>(
            $"competitions/{CompetitionId}/seasons?locale=it-IT", cancellationToken);
        var currentSeason = seasons?.Seasons?.FirstOrDefault(x => x.SeasonName == FantacalcioTime.LabelYear(year));
        if (currentSeason == null) return [];
        var players = await GetPlayersAsync(currentSeason.SeasonId, cancellationToken);
        // Prefer the current catalogue. Do not fetch a whole additional season when every requested photo is
        // already available; the previous season is a fallback, not a prerequisite for each manual refresh.
        if (requested.Count > 0 && requested.All(player => FindImagePath(player, players) != null)) return players;
        var previousSeason = seasons?.Seasons?.FirstOrDefault(x => x.SeasonName == FantacalcioTime.LabelYear(year - 1));
        if (previousSeason != null)
            players.AddRange(await GetPlayersAsync(previousSeason.SeasonId, cancellationToken));
        return players;
    }

    private async Task<List<SdpPlayer>> GetPlayersAsync(string seasonId, CancellationToken cancellationToken)
    {
        Task<PlayersResponse?> PageAsync(int page) => _httpClient.GetFromJsonAsync<PlayersResponse>(
            $"seasons/{Uri.EscapeDataString(seasonId)}/stats/players?category=General&page={page}&locale=it-IT", cancellationToken);
        var first = await PageAsync(1);
        List<SdpPlayer> players = first?.Players?.ToList() ?? [];
        if (first?.Pagination is not { IsLastPage: false } pagination) return players;
        // A season currently has dozens of pages. Fetch bounded batches rather than serially exceeding the
        // refresh endpoint's 45-second deadline. Keep page/season ordering, including current-before-previous.
        // Any failed page aborts the catalogue before a single image can be overwritten.
        var lastPage = Math.Clamp(pagination.TotalPages, 1, 100);
        for (var page = 2; page <= lastPage; page += CataloguePageConcurrency)
        {
            var batch = await Task.WhenAll(Enumerable.Range(page, Math.Min(CataloguePageConcurrency, lastPage - page + 1)).Select(PageAsync));
            foreach (var response in batch)
                if (response?.Players != null) players.AddRange(response.Players);
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
            .Where(x => IsRoleCompatible(role, x.Role))
            .Select(x => new
            {
                Player = x,
                Path = GetImagePath(x),
                NameScore = Names(x).Max(value => NameSimilarity(name, value)),
                RoleDistance = RoleDistance(role, x.Role)
            })
            .Where(x => x.Path != null)
            .Where(x => x.NameScore >= 0.84)
            .ToList();
        return candidates
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(team) && TeamNames(x.Player).Any(value => NormalizeName(value) == team))
            .ThenByDescending(x => x.NameScore)
            .ThenBy(x => x.RoleDistance)
            .FirstOrDefault()?.Path;
    }

    private static bool IsRoleCompatible(int playerRole, int sourceRole)
    {
        // Goalkeepers never cross-match with outfield roles. For outfield players,
        // allow only adjacent positions: defender-midfielder-forward.
        if (playerRole == 0 || sourceRole == 0) return true;
        if (playerRole == 1 || sourceRole == 1) return playerRole == sourceRole;
        return Math.Abs(playerRole - sourceRole) <= 1;
    }

    private static int RoleDistance(int playerRole, int sourceRole)
        => playerRole == 0 || sourceRole == 0 ? 0 : Math.Abs(playerRole - sourceRole);

    private static double NameSimilarity(string name, string? candidate)
    {
        var nameTokens = SignificantNameTokens(name).ToArray();
        var candidateTokens = SignificantNameTokens(candidate).ToArray();
        if (nameTokens.Length == 0 || candidateTokens.Length == 0) return 0;
        if (string.Join(' ', nameTokens) == string.Join(' ', candidateTokens)) return 1;

        // A source name may include an additional given name (e.g. "Romano Floriani Mussolini").
        // Ignore isolated initials (e.g. "Cissè A."). If the roster only has a surname, compare it
        // against the final source-name token (e.g. "Rahmani" vs "Amir Rrahmani").
        if (nameTokens.Length == 1)
        {
            if (candidateTokens.Length < 2) return 0;
            var surnameSimilarity = TokenSimilarity(nameTokens[0], candidateTokens[^1]);
            return surnameSimilarity >= 0.84 ? surnameSimilarity : 0;
        }
        if (candidateTokens.Length < nameTokens.Length) return 0;

        var remainingTokens = candidateTokens.ToList();
        var similarities = new List<double>(nameTokens.Length);
        foreach (var token in nameTokens)
        {
            var best = remainingTokens
                .Select((value, index) => new { Index = index, Similarity = TokenSimilarity(token, value) })
                .OrderByDescending(x => x.Similarity)
                .First();
            if (best.Similarity < 0.75) return 0;
            similarities.Add(best.Similarity);
            remainingTokens.RemoveAt(best.Index);
        }

        var extraTokenPenalty = Math.Min(0.1, remainingTokens.Count * 0.03);
        return similarities.Average() - extraTokenPenalty;
    }

    private static IEnumerable<string> SignificantNameTokens(string? name)
        => Regex.Replace(NormalizeName(name), @"[^\p{L}\p{N}\s]", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length > 1);

    private static double TokenSimilarity(string left, string right)
    {
        if (left == right) return 1;
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        for (var i = 1; i <= left.Length; i++)
        {
            var current = new int[right.Length + 1];
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var substitutionCost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + substitutionCost);
            }
            previous = current;
        }
        return 1d - (double)previous[right.Length] / Math.Max(left.Length, right.Length);
    }

    private static string? DetectImageContentType(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 12 &&
            data[..4].SequenceEqual("RIFF"u8) &&
            data.Slice(8, 4).SequenceEqual("WEBP"u8))
            return "image/webp";
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
            return "image/png";
        if (data.Length >= 3 && data[..3].SequenceEqual(new byte[] { 0xFF, 0xD8, 0xFF }))
            return "image/jpeg";
        return null;
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
