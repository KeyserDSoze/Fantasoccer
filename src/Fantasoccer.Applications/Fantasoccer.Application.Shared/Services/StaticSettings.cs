namespace Fantasoccer.Application.Shared
{
    public sealed class StaticSettings
    {
        public string Version { get; }
        public string StaticContent { get; }
        public List<CssStatisSettings> Csses { get; }
        public List<JavascriptStaticSettings> Javascripts { get; }
        public StaticSettings()
        {
            Version = "0.6.6-beta";
            StaticContent = "shared/";
            Csses = GetCsses().ToList();
            Javascripts = GetJavascripts().ToList();
        }
        public string GetStaticContentUri(string path)
        {
            return $"../../{StaticContent}{path}";
        }
        public IEnumerable<CssStatisSettings> GetCsses()
        {
            yield return new CssStatisSettings
            {
                Uri = "https://cdn.jsdelivr.net/npm/bootstrap@4.6.1/dist/css/bootstrap.min.css",
                CrossOrigin = "anonymous",
                Integrity = "sha384-zCbKRCUGaJDkqS1kPbPd7TveP5iyJE0EjAuZQTgFLD2ylzuqKfdKlfG/eSrtxUkn"
            };
            yield return new CssStatisSettings
            {
                Uri = "_content/Microsoft.FluentUI.AspNetCore.Components/css/reboot.css",
            };
            yield return new CssStatisSettings
            {
                Uri = $"{StaticContent}css/site.css?v={Version}",
            }; ;
        }
        public IEnumerable<JavascriptStaticSettings> GetJavascripts()
        {
            yield return new JavascriptStaticSettings
            {
                Uri = "_content/Microsoft.FluentUI.AspNetCore.Components/Microsoft.FluentUI.AspNetCore.Components.lib.module.js",
                IsAsync = true,
                Type = "module"
            };
            yield return new JavascriptStaticSettings
            {
                Uri = "_content/Microsoft.FluentUI.AspNetCore.Components/js/loading-theme.js",
                IsAsync = false,
                Type = "text/javascript"
            };
            yield return new JavascriptStaticSettings
            {
                Uri = $"{StaticContent}scripts/main.js?v={Version}",
                IsAsync = false,
                Type = "text/javascript"
            };
            yield return new JavascriptStaticSettings
            {
                Uri = $"{StaticContent}scripts/audiorecorder.js?v={Version}",
                IsAsync = false,
                Type = "text/javascript"
            };
            yield return new JavascriptStaticSettings
            {
                Uri = $"{StaticContent}scripts/PushNotification.js?v={Version}",
                IsAsync = false,
                Type = "text/javascript"
            };
            yield return new JavascriptStaticSettings
            {
                Uri = "_content/Rystem.Authentication.Social.Blazor/socialauthentications.js",
                IsAsync = false,
                Type = "text/javascript"
            };
        }
    }
    public sealed class CssStatisSettings
    {
        public string Uri { get; set; }
        public string? Integrity { get; set; }
        public string? CrossOrigin { get; set; }
    }
    public sealed class JavascriptStaticSettings
    {
        public string Uri { get; set; }
        public bool IsAsync { get; set; }
        public string Type { get; set; }
    }
}
