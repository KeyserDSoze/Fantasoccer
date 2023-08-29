using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;
using RepositoryFramework;

namespace Fantasoccer.Tests.Tests.Services
{
    public class VotingTest
    {
        private readonly IOfficialVote _official;
        private readonly ILiveVote _live;
        private readonly ISerieACalendar _serieACalendar;

        public VotingTest(IOfficialVote official,
            ILiveVote live,
            ISerieACalendar serieACalendar)
        {
            _official = official;
            _live = live;
            _serieACalendar = serieACalendar;
        }
        [Fact]
        public async Task FetchOfficialAsync()
        {
            var players = await _official.GetAsync(FantacalcioTime.ActualYear, 1).NoContext();
            Assert.NotNull(players);
            Assert.True(players.Count > 60);
            Assert.Equal(20, players.GroupBy(x => x.RealTeam).Count());
        }
        [Fact]
        public async Task FetchLiveAsync()
        {
            var calendar = await _serieACalendar.GetAsync().NoContext();
            var isLive = calendar.IsLive;
            var players = await _live.GetAsync(FantacalcioTime.ActualYear, 2).NoContext();
            Assert.NotNull(players);
            if (!isLive)
                Assert.Empty(players);
            else
                Assert.NotEmpty(players);
            if (isLive)
            {
                Assert.True(players.Count > 60);
                Assert.Equal(20, players.GroupBy(x => x.RealTeam).Count());
            }
        }
    }
}
