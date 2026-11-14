namespace Fantasoccer.Business.Services
{
    internal enum BrowserRequestProfile
    {
        // Same identity as Chrome (user agent, client hints), nothing about how the request was made (images, mixed calls).
        Common,
        // A page loaded in the tab: HTML, navigate, coming from the page itself.
        Document,
        // A fetch/XHR call of the site's own scripts: JSON, cors.
        Api,
    }

    // Every outgoing provider call looks like it comes from a real Chrome. Requests that already
    // declare their own Sec-Fetch-Mode (the CORS calls of LiveVote) keep their headers.
    internal sealed class BrowserLikeHandler(BrowserRequestProfile profile) : DelegatingHandler
    {
        public const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";
        public const string SecChUa = "\"Chromium\";v=\"124\", \"Google Chrome\";v=\"124\", \"Not-A.Brand\";v=\"99\"";
        public const string AcceptLanguage = "it-IT,it;q=0.9,en-US;q=0.8,en;q=0.7";

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers;
            headers.TryAddWithoutValidation("Sec-Ch-Ua", SecChUa);
            headers.TryAddWithoutValidation("Sec-Ch-Ua-Mobile", "?0");
            headers.TryAddWithoutValidation("Sec-Ch-Ua-Platform", "\"Windows\"");
            if (profile != BrowserRequestProfile.Common && request.RequestUri is { } uri && !headers.Contains("Sec-Fetch-Mode"))
            {
                if (profile == BrowserRequestProfile.Document)
                {
                    headers.Remove("Accept");
                    headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7");
                    headers.TryAddWithoutValidation("Cache-Control", "max-age=0");
                    headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
                    headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-origin");
                    headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
                    headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");
                    headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
                    // The call comes from the page itself, as a reload would.
                    headers.Referrer ??= new Uri(uri.GetLeftPart(UriPartial.Path));
                }
                else
                {
                    headers.Remove("Accept");
                    headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
                    headers.TryAddWithoutValidation("Sec-Fetch-Site", "same-site");
                    headers.TryAddWithoutValidation("Sec-Fetch-Mode", "cors");
                    headers.TryAddWithoutValidation("Sec-Fetch-Dest", "empty");
                }
            }
            return base.SendAsync(request, cancellationToken);
        }
    }
}
