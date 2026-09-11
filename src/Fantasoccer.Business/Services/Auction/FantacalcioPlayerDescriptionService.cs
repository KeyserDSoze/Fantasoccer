using System.Net;
using System.Text.RegularExpressions;
using Fantasoccer.Domain;

namespace Fantasoccer.Business
{
    public sealed class FantacalcioPlayerDescriptionService
    {
        private static readonly Regex s_rowRegex = new("<tr class=\"player-row[^>]*>(?<row>.*?)(?=<tr class=\"player-row|</tbody>)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex s_nameRegex = new("<th class=\"player-name\".*?<span>(?<name>.*?)</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex s_linkRegex = new("<a[^>]+class=\"player-name player-link\"[^>]+href=\"(?<url>[^\"]+)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex s_sectionRegex = new("<section[^>]+id=\"player-description\"[^>]*>(?<content>.*?)</section>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private static readonly Regex s_tipRegex = new("<strong>\\s*(?<type>PRO|CONTRO)\\s*</strong>\\s*:\\s*(?<text>.*?)(?=<strong>|</li>)", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);
        private readonly HttpClient _httpClient;
        private readonly SemaphoreSlim _linksLock = new(1, 1);
        private Dictionary<string, string>? _links;
        private readonly Dictionary<string, PlayerDescription?> _cache = new(StringComparer.OrdinalIgnoreCase);

        public FantacalcioPlayerDescriptionService(IHttpClientFactory httpClientFactory)
        {
            _httpClient = httpClientFactory.CreateClient("all-players");
        }

        public async Task EnrichAsync(StatPlayer player)
        {
            try
            {
                if (player.Description != null) return;
                var links = await GetLinksAsync();
                if (!links.TryGetValue(player.Name, out var url)) return;
                if (!_cache.TryGetValue(url, out var description))
                {
                    description = await LoadDescriptionAsync(url);
                    _cache[url] = description;
                }
                player.Description = description;
            }
            catch (HttpRequestException)
            {
                // Auction flow must continue if the external editorial site is unavailable.
            }
        }

        private async Task<Dictionary<string, string>> GetLinksAsync()
        {
            if (_links != null) return _links;
            await _linksLock.WaitAsync();
            try
            {
                if (_links != null) return _links;
                var html = await _httpClient.GetStringAsync("https://www.fantacalcio.it/quotazioni-fantacalcio");
                _links = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (Match row in s_rowRegex.Matches(html))
                {
                    var name = s_nameRegex.Match(row.Groups["row"].Value).Groups["name"].Value;
                    var url = s_linkRegex.Match(row.Groups["row"].Value).Groups["url"].Value;
                    if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(url))
                        _links[WebUtility.HtmlDecode(StripTags(name)).Trim()] = WebUtility.HtmlDecode(url);
                }
                return _links;
            }
            finally
            {
                _linksLock.Release();
            }
        }

        private async Task<PlayerDescription?> LoadDescriptionAsync(string url)
        {
            var html = await _httpClient.GetStringAsync(url);
            var content = s_sectionRegex.Match(html).Groups["content"].Value;
            if (string.IsNullOrWhiteSpace(content)) return null;
            var description = new PlayerDescription();
            foreach (Match tip in s_tipRegex.Matches(content))
            {
                var text = WebUtility.HtmlDecode(StripTags(tip.Groups["text"].Value)).Trim();
                if (tip.Groups["type"].Value.Equals("PRO", StringComparison.OrdinalIgnoreCase)) description.Top = text;
                else description.Down = text;
            }
            return description.Top == null && description.Down == null ? null : description;
        }

        private static string StripTags(string value) => Regex.Replace(value, "<[^>]+>", " ").Replace("&nbsp;", " ");
    }
}
