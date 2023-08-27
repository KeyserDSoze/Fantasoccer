using Fantasoccer.Engine.IngestionData;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Services.FormationParsers
{
    public class FormationParserTests
    {
        private readonly IFactory<IFormationParser> _factory;
        private readonly HttpClient _httpClient;
        public FormationParserTests(IFactory<IFormationParser> factory,
            IHttpClientFactory httpClientFactory)
        {
            _factory = factory;
            _httpClient = httpClientFactory.CreateClient("base");
        }
        [Theory]
        [InlineData("Fantagazzetta")]
        [InlineData("Gazzetta")]
        [InlineData("Sky")]
        [InlineData("Mediaset")]
        [InlineData("Injury")]
        public async Task TestParserAsync(string name)
        {
            var parser = _factory.Create(name);
            var players = await parser.GetPlayersAsync(_httpClient);
            Assert.NotEmpty(players);
        }
    }
}
