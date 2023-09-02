using Fantasoccer.Business;
using Fantasoccer.Business.IngestionData;

namespace Fantasoccer.Tests.Tests.Services
{
    public class AllPlayersTest
    {
        private readonly IAllPlayers _allPlayers;
        public AllPlayersTest(IAllPlayers allPlayers)
        {
            _allPlayers = allPlayers;
        }
        [Fact]
        public async Task FetchAsync()
        {
            var players = await _allPlayers.GetAsync();
            Assert.NotNull(players);
            Assert.True(players.Count > 100);
            Assert.Equal(20, players.GroupBy(x => x.RealTeam.Name).Count());
        }
    }
}
