using Microsoft.Extensions.Configuration;

namespace Fantasoccer.Application.Shared
{
    public sealed class ImageUriHelper
    {
        private readonly string _baseUriStorage;
        public ImageUriHelper(IConfiguration configuration, StaticSettings staticSettings)
        {
            _baseUriStorage = configuration["Uri:Storage"]!;
            s_placeholderPlayerImageUri = $"this.src ='{staticSettings.GetStaticContentUri("static/nogioc.jpg")}'";
        }
        public string GetPlayerImageUri(Player player)
            => $"https://{_baseUriStorage}/images/players/{player.Key}.jpg";
        private readonly string s_placeholderPlayerImageUri;
        public string GetPlaceholderPlayerImageUri()
            => s_placeholderPlayerImageUri;
        public string GetTeamImageUri(string groupId, int year, string id)
            => $"https://{_baseUriStorage}/logos/{groupId}/{year}/{id}.png";
        private const string PlaceholderTeamImageUri = "this.src ='https://upload.wikimedia.org/wikipedia/commons/9/99/Scudetto.svg'";
        public string GetPlaceholderTeamImageUri()
            => PlaceholderTeamImageUri;
    }
}
