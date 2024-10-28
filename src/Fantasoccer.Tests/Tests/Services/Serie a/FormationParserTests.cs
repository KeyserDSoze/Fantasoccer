using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Services
{
    public class FormationParserTests
    {
        private readonly IFactory<IFormationParser> _factory;
        private readonly ISerieAInformation _serieACalendar;
        private readonly IRepository<RealCalendar, int> _repository;
        public FormationParserTests(IFactory<IFormationParser> factory, ISerieAInformation serieACalendar, IRepository<RealCalendar, int> repository)
        {
            _factory = factory;
            _serieACalendar = serieACalendar;
            _repository = repository;
        }
        [Theory]
        [InlineData("Fantagazzetta")]
        [InlineData("Gazzetta")]
        [InlineData("Sky")]
        [InlineData("Mediaset")]
        public async Task TestParserAsync(string name)
        {
            //var calendar = await _serieACalendar.GetCalendarAsync().NoContext();
            //await _repository.InsertAsync(FantacalcioTime.ActualYear, calendar).NoContext();
            var parser = _factory.Create(name);
            var players = await parser.GetPlayersAsync();
            Assert.NotEmpty(players);
            Assert.True(players.Count > 100);
            Assert.Equal(20, players.GroupBy(x => x.RealTeam.Name).Count());
        }
        [Theory]
        [InlineData("Injury")]
        public async Task TestInjuryParserAsync(string name)
        {
            var calendar = await _serieACalendar.GetCalendarAsync().NoContext();
            await _repository.InsertAsync(FantacalcioTime.ActualYear, calendar).NoContext();
            var parser = _factory.Create(name);
            var players = await parser.GetPlayersAsync();
            Assert.NotEmpty(players);
            Assert.True(players.Count > 10);
            Assert.True(players.GroupBy(x => x.RealTeam.Name).Count() > 2);
        }
    }
}
