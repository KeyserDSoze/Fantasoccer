namespace Fantasoccer.Applications.App
{
    public sealed class ImageUriHelper
    {
        private readonly string _baseUriStorage;
        public ImageUriHelper(IConfiguration configuration)
        {
            _baseUriStorage = configuration["Uri:Storage"]!;
        }
        public string GetPlayerImageUri(Player player)
            => $"https://{_baseUriStorage}/images/players/{player.Key}.jpg";
        private static readonly string s_placeholderPlayerImageUri = "this.src ='./static/nogioc.jpg'";
        public string GetPlaceholderPlayerImageUri()
            => s_placeholderPlayerImageUri;
        public string GetTeamImageUri(string groupId, int year, string id)
            => $"https://{_baseUriStorage}/logos/{groupId}/{year}/{id}.png";
        private static readonly string s_placeholderTeamImageUri = "this.src ='https://upload.wikimedia.org/wikipedia/commons/9/99/Scudetto.svg'";
        public string GetPlaceholderTeamImageUri()
            => s_placeholderTeamImageUri;
    }
}
