using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Business
{
    internal sealed class VotePlayerGenerator : IVotePlayerGenerator
    {
        private readonly IOfficialVote _finalVote;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _repository;

        public VotePlayerGenerator(IOfficialVote finalVote,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory)
        {
            _finalVote = finalVote;
            _repository = factory.Create("official");
        }
        public async ValueTask<int> GenerateAndSaveAsync(int year, int untilThisDay, int liveDay = 39, bool force = false)
        {
            var counter = 0;
            for (var serieADay = untilThisDay; serieADay > 0; serieADay--)
            {
                var isLive = liveDay == serieADay;
                var key = new YearDayKey
                {
                    Day = serieADay,
                    Year = year,
                };
                var exist = await _repository.ExistAsync(key);
                if (!exist || isLive || force)
                {
                    var players = await _finalVote.GetAsync(FantacalcioTime.ActualYear, serieADay).NoContext();
                    if (exist)
                        await _repository.UpdateAsync(key, new VotedRealPlayerWrapper
                        {
                            Players = players.Where(x => !string.IsNullOrWhiteSpace(x.Key)).ToList(),
                        });
                    else
                        await _repository.InsertAsync(key, new VotedRealPlayerWrapper
                        {
                            Players = players.Where(x => !string.IsNullOrWhiteSpace(x.Key)).ToList(),
                        });
                    counter++;
                }
            }
            return counter;
        }
    }
}
