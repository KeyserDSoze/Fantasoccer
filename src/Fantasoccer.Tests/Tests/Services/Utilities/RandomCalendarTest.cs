using System.Population.Random;
using Fantasoccer.Business;

namespace Fantasoccer.Tests
{
    public class RandomCalendarTest
    {
        private readonly RandomCalendar _randomCalendar;
        private readonly IPopulation<AnnualTeam> _populationService;

        public RandomCalendarTest(RandomCalendar randomCalendar, IPopulation<AnnualTeam> populationService)
        {
            _randomCalendar = randomCalendar;
            _populationService = populationService;
        }
        [Theory]
        [InlineData(10, LeagueType.League, 36, 1)]
        [InlineData(16, LeagueType.Cup, 12, 4)]
        public void Create(int numberOfTeams, LeagueType leagueType, int numberOfDays, int rounds)
        {
            var calendar = _randomCalendar.GetRandomCalendar(2, new AnnualLeague
            {
                Id = Guid.NewGuid().ToString(),
                Settings = new LeagueSetting
                {

                },
                Type = leagueType,
                Teams = _populationService.Populate(numberOfTeams)
            });

            Assert.NotNull(calendar);
            Assert.Equal(calendar.Rounds.Count, rounds);
            Assert.Equal(calendar.Rounds.First().Value.Count, numberOfDays);
        }
    }
}
