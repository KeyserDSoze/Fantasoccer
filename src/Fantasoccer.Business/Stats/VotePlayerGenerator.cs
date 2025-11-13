using Fantasoccer.Business.IngestionData;
using Microsoft.Extensions.DependencyInjection;
using RepositoryFramework;

namespace Fantasoccer.Business
{
    internal sealed class VotePlayerGenerator : IVotePlayerGenerator
    {
        private readonly IOfficialVote _finalVote;
        private readonly IRepository<RealCalendar, int> _realCalendar;
        private readonly IRepository<VotedRealPlayerWrapper, YearDayKey> _repository;

        public VotePlayerGenerator(IOfficialVote finalVote,
             IRepository<RealCalendar, int> realCalendar,
            IFactory<IRepository<VotedRealPlayerWrapper, YearDayKey>> factory)
        {
            _finalVote = finalVote;
            _realCalendar = realCalendar;
            _repository = factory.Create("official")!;
        }
        public async ValueTask<bool> GenerateAndSaveAsync(int year, int untilThisDay, bool force = false, int withMax = 1)
        {
            var calendar = await _realCalendar.GetAsync(year);
            var counter = 0;
            for (var serieADay = untilThisDay; serieADay >= withMax; serieADay--)
            {
                var hasAllVotes = true;
                var key = new YearDayKey
                {
                    Day = serieADay,
                    Year = year,
                };
                var exist = await _repository.ExistAsync(key);
                var dayOfCalendar = calendar!.Days.First(x => x.SerieADay == serieADay);
                var numberOfTeamsThatHavePlayedThisDay = dayOfCalendar.Games.Count(x => !x.Delayed) * 2;
                if (exist)
                {
                    var votes = await _repository.GetAsync(key);
                    var numberOfTeamsThatHaveInBasket = votes!.Players.Where(x => x.RealTeam != null).GroupBy(x => x.RealTeam.Name).Select(x => x.Key).Distinct().Count();
                    hasAllVotes = numberOfTeamsThatHaveInBasket >= numberOfTeamsThatHavePlayedThisDay;
                }
                if (!exist || !hasAllVotes || force)
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
                    var numberOfTeamsThatHaveInBasket = players.Where(x => x.RealTeam != null).GroupBy(x => x.RealTeam.Name).Select(x => x.Key).Distinct().Count();
                    hasAllVotes = numberOfTeamsThatHaveInBasket >= numberOfTeamsThatHavePlayedThisDay;
                    if (hasAllVotes)
                        counter++;
                }
            }
            return counter > 0;
        }
    }
}
