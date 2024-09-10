using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Fantasoccer.Business.IngestionData;

namespace Fantasoccer.Tests.Tests.Votes
{
    public class LiveTest
    {
        private readonly ILiveVote _liveVote;
        public LiveTest(ILiveVote liveVote)
        {
            _liveVote = liveVote;
        }
        [Fact]
        public async Task CheckAsync()
        {
            var players = await _liveVote.GetAsync(19, 3);
            Assert.NotNull(players);
            Assert.True(players.Count > 100);
        }
    }
}
