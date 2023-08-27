using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.DependencyInjection;

namespace Fantasoccer.Tests.Tests.Services.FormationParsers
{
    public class FormationParserTests
    {
        private readonly IFactory<IFormationParser> _factory;
        public FormationParserTests(IFactory<IFormationParser> factory)
        {
            _factory = factory;
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
            var players = await parser.GetPlayersAsync();
            Assert.NotEmpty(players);
        }
    }
}
